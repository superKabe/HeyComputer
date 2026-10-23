# Orchestration Governance

## Purpose and scope

This document defines the P0 governance layer for delegated work. The Conversation Agent remains the only user-facing personality, the Orchestration Agent is the only component allowed to create or assign tasks, the Context Agent may propose memory updates from verified outcomes, and Worker Agents are disposable executors. The governance layer must prevent silent overspending, permission escalation, recursive fan-out, duplicate side effects, and unsupported claims of completion while keeping the conversation responsive.

The first implementation should be a provider-neutral .NET library used by the desktop process. It does not require a separate service. Use asynchronous interfaces, immutable command records, explicit state transitions, `CancellationToken`, UTC timestamps, and durable writes before dispatching paid or side-effecting work.

## Non-negotiable invariants

1. Only the Orchestration Agent may create child tasks, assign workers, select providers, or request expanded capabilities.
2. A worker receives only the objective, bounded context, explicit capabilities, budget, deadline, and result contract needed for its task. It never receives unrestricted application authority by default.
3. Workers cannot change personality, durable memory, permissions, provider credentials, budget settings, or governance policy. They may return proposals for the owning component to review.
4. A worker's success claim is not completion. The Orchestration Agent must validate required evidence before changing a task to `Succeeded`.
5. Paid work must not start until its task and reserved usage are durably recorded. Consequential actions must not execute until their approval is durably recorded and still valid.
6. Low-budget work always pauses for user confirmation. Automatic model downgrading may be proposed, but it does not bypass the confirmation boundary.
7. The Conversation Agent can report, cancel, or reprioritize work but does not wait synchronously for a worker.
8. Provider text, webpage content, files, and worker results are untrusted data. They cannot grant permissions or issue orchestration commands.

## Runtime boundaries

Implement governance behind a small set of interfaces so storage, providers, and worker runtimes remain replaceable:

```csharp
public interface ITaskLedger
{
    Task<TaskRecord?> GetAsync(Guid taskId, CancellationToken ct);
    Task<IReadOnlyList<TaskRecord>> QueryAsync(TaskQuery query, CancellationToken ct);
    Task AppendTransitionAsync(TaskTransition transition, CancellationToken ct);
}

public interface IUsageLedger
{
    Task<BudgetSnapshot> GetSnapshotAsync(BudgetScope scope, CancellationToken ct);
    Task<UsageReservation> ReserveAsync(UsageProposal proposal, CancellationToken ct);
    Task ReconcileAsync(UsageReconciliation actual, CancellationToken ct);
    Task ReleaseAsync(Guid reservationId, string reason, CancellationToken ct);
}

public interface ICapabilityBroker
{
    Task<CapabilityDecision> AuthorizeAsync(CapabilityRequest request, CancellationToken ct);
    Task<CapabilityLease?> IssueLeaseAsync(CapabilityDecision decision, CancellationToken ct);
    Task RevokeAsync(Guid leaseId, string reason, CancellationToken ct);
}

public interface IResultValidator
{
    Task<ValidationReport> ValidateAsync(TaskRecord task, WorkerResult result, CancellationToken ct);
}
```

Commands should include an `OperationId` for idempotency and an `ExpectedVersion` for optimistic concurrency. Ledger implementations reject stale transitions rather than resolving them with last-write-wins behavior.

## Task ledger

The task ledger is the canonical record of intended work, execution attempts, approvals, evidence, and results. Store mutable query state transactionally in SQLite and write a user-exportable JSON Lines event stream as the inspectable audit representation. SQLite is operational state, not hidden memory: every record must be viewable through the application and exportable without provider-specific objects.

### Task record schema

```csharp
public sealed record TaskRecord(
    Guid TaskId,
    Guid? ParentTaskId,
    Guid RootTaskId,
    int Depth,
    string Title,
    string Objective,
    string RequestedBy,              // conversation, user, scheduler, or parent task
    TaskOwner Owner,                 // always Orchestrator for worker tasks
    TaskStatus Status,
    int Version,
    string DeduplicationKey,
    IReadOnlyList<Guid> Dependencies,
    WorkerAssignment? Assignment,
    ResultContract ResultContract,
    BudgetEnvelope Budget,
    IReadOnlyList<CapabilityGrant> GrantedCapabilities,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? NotBeforeUtc,
    DateTimeOffset DeadlineUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    CancellationRecord? Cancellation,
    WorkerResult? Result,
    ValidationReport? Validation,
    FailureRecord? Failure);
```

`Objective` describes a testable outcome, not an open-ended persona prompt. `DeduplicationKey` is derived from the normalized objective, relevant resource identities, and root request so retries reuse the same logical task. `Dependencies` must all be successfully validated before dispatch. `ResultContract` names the expected structured schema, required evidence kinds, and acceptance checks. User-visible titles and summaries are stored separately from untrusted raw worker output.

### Assignment and attempt schema

```csharp
public sealed record WorkerAssignment(
    string WorkerAdapterId,
    string ProviderId,
    string ModelId,
    string RouteReason,
    Guid AttemptId,
    int AttemptNumber,
    DateTimeOffset LeaseExpiresAtUtc,
    string InputDigest,
    IReadOnlyList<string> ContextSourceIds);

public sealed record TaskAttempt(
    Guid AttemptId,
    Guid TaskId,
    int AttemptNumber,
    AttemptStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? HeartbeatAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? ProviderRequestIdHash,
    string? CheckpointReference,
    FailureRecord? Failure);
```

Provider request identifiers are hashed or redacted in ordinary logs. A worker lease must expire unless renewed by a heartbeat. Expiration marks the attempt `Indeterminate` until the orchestrator determines whether paid work or a side effect may already have occurred.

### Task lifecycle

Use this state machine and reject all other transitions:

- `Draft` → `Planned` after decomposition and duplicate detection.
- `Planned` → `AwaitingApproval`, `Blocked`, `Queued`, or `Cancelled` after routing, budget, dependency, and permission evaluation.
- `AwaitingApproval` → `Queued` only from an explicit, unexpired approval; otherwise `Cancelled` or `Blocked`.
- `Queued` → `Running` only after durable usage reservation and capability lease issuance.
- `Running` → `Validating`, `Paused`, `Failed`, `Cancelled`, or `Indeterminate`.
- `Validating` → `Succeeded`, `NeedsReview`, `Queued` for a bounded retry, or `Failed`.
- `Paused` → `Queued`, `Cancelled`, or `Failed`.
- Terminal states are `Succeeded`, `Failed`, and `Cancelled`. `NeedsReview` and `Indeterminate` require resolution and cannot be reported as success.

Every transition is an append-only `TaskTransition` containing task ID, prior and next state, expected version, actor type, sanitized reason code, timestamp, correlation ID, and optional approval, reservation, capability lease, or evidence references.

## Usage ledger

The usage ledger records reservations before work and reconciles them with provider-reported usage afterward. It must distinguish measured cost from estimated cost and must never present an estimate as a provider invoice. Track money and native units because providers may bill by tokens, audio duration, requests, images, tool calls, or compute time.

```csharp
public sealed record UsageEntry(
    Guid EntryId,
    Guid TaskId,
    Guid AttemptId,
    string ProviderId,
    string ModelId,
    UsageEntryKind Kind,              // Reservation, Actual, Estimate, Adjustment, Release
    IReadOnlyDictionary<string, decimal> Units,
    decimal? CostAmount,
    string? CurrencyCode,
    UsageConfidence Confidence,       // ProviderReported, Calculated, Estimated, Unknown
    string PricingVersion,
    DateTimeOffset OccurredAtUtc,
    string? ProviderReceiptHash,
    string ReasonCode);

public sealed record BudgetEnvelope(
    BudgetScope Scope,
    decimal? HardLimit,
    decimal? WarningThreshold,
    decimal? PerTaskLimit,
    string CurrencyCode,
    IReadOnlyDictionary<string, decimal> NativeUnitLimits,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc);
```

Maintain budgets for the task, conversation session, provider window, and user-configured daily or monthly period. Available budget equals the hard limit minus reconciled actuals minus open reservations. If current provider usage cannot be measured, reserve against the conservative estimated maximum. Pricing data carries a fetch timestamp and version; stale or unknown pricing lowers confidence and can itself trigger approval.

Reservations are released when a task is cancelled before dispatch. After dispatch, reservations remain until reconciliation or a documented timeout, at which point they become an estimate rather than silently disappearing. Adjustments are append-only and must reference the corrected entry.

## Model-routing policy

Routing is a constrained decision, not a universal “best model” ranking. First remove routes that lack required capabilities, violate the task's privacy boundary, cannot meet the deadline, have unavailable credentials, or exceed a hard budget. Then score eligible routes using task-specific quality, reliability history, latency, context capacity, locality, and projected cost.

Use a deterministic policy input and record the selected route plus the rejected alternatives' reason codes. The initial score can be implemented as weighted normalized values:

```text
routeScore = quality + reliability + latencyFit + privacyFit + costFit
```

Weights come from the task class and user settings, not from worker output. Examples: simple classification favors low latency and low cost; coding favors tool support and repository performance; sensitive summarization favors local processing when available; interactive speech favors time to first response. A user-pinned provider is preferred only when it remains capable, permitted, and inside budget.

Fallback routes are planned before dispatch. A provider failure may switch to an eligible fallback only when the fallback does not expand data disclosure, capabilities, or approved cost. Otherwise the task moves to `AwaitingApproval`. The routing policy should expose a concise explanation such as “selected for browser tools and lower projected cost,” without leaking internal model reasoning.

## Concurrency and fan-out

The MVP defaults to three concurrent worker attempts globally, no more than two paid cloud workers at once, one active worker per logical task, and one writer per resource scope. These are configurable downward by the user but cannot be raised by a worker. Provider rate limits and user budgets can impose stricter limits.

Only the Orchestration Agent may fan out. Workers may recommend subtasks in their structured result but cannot dispatch them. For the first vertical slice, maximum task depth is one and maximum children per root task is three. Later releases may allow deeper plans, but every generation must have a configured depth limit, child limit, total-task limit, and reserved aggregate budget before dispatch.

Before creating a child, the orchestrator checks the deduplication key against active and recently completed tasks, verifies that the expected information gain justifies another worker, and computes aggregate worst-case usage. Fan-out must stop when any limit is reached, the user cancels, the objective is already satisfied, results converge, or remaining work would enter a low-budget condition.

Tasks sharing an exclusive resource lock, such as the same file, browser session, or mutable application document, run serially unless the adapter can prove conflict-free operations. The queue should use weighted fairness so a long background plan cannot starve direct user requests.

## Low-budget confirmation

A task enters `AwaitingApproval` before further paid work when any of these conditions is true:

- Available budget is at or below the configured warning threshold.
- The task's conservative reservation would exceed the available safe budget or its per-task limit.
- Proposed fan-out would cross a configured worker-count, request-count, token, duration, or monetary threshold.
- Usage or pricing is unknown enough that the system cannot establish the work remains within the approved envelope.
- A retry or fallback would exceed the original reservation.

The Conversation Agent presents one concise decision containing the task purpose, planned worker count, selected provider or model class, estimated range with confidence, remaining known budget, and cheaper alternatives. The choices are approve once, reduce scope, choose a cheaper eligible route, or cancel. Silence is never approval.

Approval is scoped to the exact plan digest, maximum cost or native-unit envelope, capability set, and expiration time. Any material change invalidates it. After approval, reserve the entire authorized envelope before dispatch. If budget becomes low during execution, finish only already-issued atomic operations, pause before the next provider call or side effect, and ask again. Do not automatically continue merely because switching to a cheaper model appears possible.

## Worker capability model

A capability is a structured grant, not a textual instruction. It identifies the action, resource scope, risk class, constraints, issuer, expiration, and task/attempt to which it is bound.

```csharp
public sealed record CapabilityGrant(
    Guid GrantId,
    Guid TaskId,
    Guid AttemptId,
    CapabilityAction Action,
    ResourceScope Scope,
    RiskClass Risk,
    IReadOnlyDictionary<string, string> Constraints,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    Guid? ApprovalId);
```

The broker uses deny-by-default and exact scope matching. Examples of scope constraints include allowed directory roots, file patterns, domains, application identities, repository, command allowlist, maximum bytes, and read-only versus write. Capability leases are short-lived, task-bound, non-transferable, revocable, and checked at execution time. Text generated by a model cannot be converted into a grant without policy evaluation.

Workers never receive raw credentials. Provider and integration adapters perform authorized operations using protected credential references. Secrets, permission policy, usage limits, personality profiles, memory files, audit files, and the governance database are protected resources that workers cannot write. The Context Agent alone may submit memory change proposals, and a trusted memory service applies accepted changes with provenance.

## Permission and confirmation matrix

| Category | Examples | Default behavior | Confirmation rule |
| --- | --- | --- | --- |
| Internal reasoning | Planning, routing, formatting, validation against supplied data | Allow within task budget | No action confirmation; budget rules still apply |
| Read-only observation | Read a user-selected project, inspect accessibility tree, query an approved API, view a named browser tab | Allow only inside an explicit scope; show active access | Confirm first use or scope expansion; enterprise policy may require every use |
| Reversible local change | Create a new file in an approved workspace, edit through a patch, change a nonsecurity preference | Allow only with preview, backup or transactional rollback, and resource lock | MVP confirms the plan before the first write; a future remembered grant may cover a narrow scope |
| Execution | Run a build, tests, or allowlisted command in an approved workspace | Sandboxed or constrained execution with timeout and output limits | Confirm when command class or scope is new, elevated, networked, or materially costly |
| External communication | Send or modify email, chat, posts, calendar invitations, forms, or support tickets | Block until exact content, recipients, and channel are previewed | Always confirm immediately before send |
| Financial or legal commitment | Purchase, subscription, trade, donation, contract, acceptance of terms | Block | Always confirm each final action; keep out of MVP |
| Destructive or hard-to-reverse change | Delete data, overwrite without recovery, close accounts, revoke access, publish releases | Prefer reversible alternative; otherwise block | Always confirm exact target and consequence immediately before execution |
| Credentials and identity | Reveal credentials, authenticate, approve OAuth/device login, access password stores, impersonate the user | Workers cannot read secrets; trusted adapter may request use | User performs or explicitly approves each sensitive boundary; never expose secret values |
| Security or system administration | Firewall, antivirus, users, privileges, startup policy, system-wide install | Block by default | Always confirm with elevated, narrowly scoped flow; generally deferred |
| Governance and identity | Change personality, memory canon, permissions, budgets, audit history, or orchestration policy | Workers cannot perform | Dedicated trusted workflow; user confirmation required for consequential changes |

“Remember this permission” is allowed only for low-risk, narrow, revocable scopes and must show an expiration or persistent setting. It is never available for sending messages, purchases, destructive actions, credentials, security changes, or governance changes.

## Result and evidence validation

Every `ResultContract` declares a schema, acceptance criteria, evidence requirements, and validator strategy before dispatch. A worker returns structured data plus evidence references; it does not choose how its own result is validated.

```csharp
public sealed record WorkerResult(
    string SchemaId,
    JsonElement Payload,
    IReadOnlyList<EvidenceItem> Evidence,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> UnresolvedItems,
    string OutputDigest);

public sealed record EvidenceItem(
    EvidenceKind Kind,
    string Reference,
    string Digest,
    DateTimeOffset ObservedAtUtc,
    string Producer,
    EvidenceSensitivity Sensitivity);
```

Validation proceeds in four layers: parse the declared schema; verify evidence exists, is fresh, and matches its digest; run deterministic checks such as file existence, tests, API status, or UI state; then evaluate task-specific acceptance criteria. Model-based review may supplement but never replace available deterministic checks. Evidence captured before a consequential action does not prove the action completed afterward.

The validator returns `Verified`, `PartiallyVerified`, `Contradicted`, or `Unverifiable`, with individual check results. Only `Verified` can transition to `Succeeded`. Partial success moves to `NeedsReview` or creates a new bounded task for the missing portion. User-facing summaries must distinguish verified facts, estimates, and unresolved claims.

## Failure recovery

Persist the task transition, usage reservation, capability decision, and dispatch intent in one local transaction using an outbox pattern. A dispatcher delivers committed outbox messages and records the attempt lease. This prevents a crash between “charged or authorized” and “task recorded.” Adapters should pass stable idempotency keys whenever providers or action targets support them.

On application restart:

1. Reload nonterminal tasks, open reservations, approvals, leases, and outbox entries.
2. Revoke expired capability leases and prevent new side effects from stale workers.
3. Query provider or adapter status using the idempotency key where supported.
4. Resume only operations proven not to have executed, or operations explicitly designed as repeatable.
5. Mark uncertain external or destructive outcomes `Indeterminate` and ask the user to inspect or approve a recovery action. Never blindly retry them.
6. Reconcile known usage, retain conservative estimates for unknown usage, and release only reservations proven unused.

Retry only transient failures, with capped exponential backoff and jitter. The default is two retries for read-only, idempotent work and zero automatic retries for consequential side effects. Authentication failure, policy denial, invalid input, budget exhaustion, conflicting writes, and deterministic validation failure are not transient. Circuit-break a failing provider or adapter and route to a fallback only under the routing and approval rules above.

For file edits, require precondition hashes and produce a patch or recoverable backup. If the target changed, stop with a conflict instead of overwriting. Cancellation is cooperative first, then lease revocation and adapter termination; completion arriving after cancellation is recorded but cannot silently change the terminal state or trigger dependent tasks.

## Audit requirements

The local audit trail must answer: who requested the work, what was planned, which worker and model were selected, what data classes left the device, what capabilities were granted, what approvals occurred, what usage was reserved and measured, what actions executed, what evidence was validated, and how the task ended. Record structured metadata and digests, not hidden reasoning or raw secrets.

Audit events are append-only, monotonically sequenced, and hash-chained per installation so accidental edits are detectable. Each event contains schema version, event ID, task and attempt references, actor class, event type, UTC timestamp, sanitized summary, data-disclosure categories, capability/approval references, usage references, result or evidence digests, and previous-event hash. Signing with a key protected by Windows is a later hardening option; the MVP must not imply that a local hash chain prevents a machine owner or malware from rewriting all history.

Default audit data excludes raw audio, transcript text, prompts, responses, file contents, screenshots, credentials, and provider payloads. Content required as task evidence lives in its governed source or encrypted evidence store and is referenced by digest. The UI must support filtering by task, provider, capability, and time; inspecting approvals and disclosed data classes; exporting redacted JSON Lines; and deleting data according to the retention policy. Deletion creates a tombstone event and removes referenced content where possible without falsifying retained usage or security history.

Use versioned schemas and migration tests. Audit write failure blocks new paid work and consequential actions because unlogged execution violates governance; harmless local conversation may continue with a visible degraded-state warning. Protect audit and ledger files with per-user filesystem permissions, atomic writes, backups, and corruption recovery tests.

## MVP implementation order

1. Define the enums and immutable records above in a provider-neutral governance project.
2. Implement SQLite task, transition, attempt, usage, approval, capability, evidence, and outbox tables with optimistic concurrency.
3. Add JSON Lines audit export and redaction tests.
4. Implement deterministic routing for one harmless worker adapter and enforce global concurrency, depth, child-count, deduplication, and budget limits.
5. Implement one low-budget approval flow and one narrow read-only capability through the broker.
6. Add schema and deterministic evidence validation before reporting completion.
7. Prove restart recovery by terminating the application during queued, running, validating, and approval states without duplicate paid calls or actions.

## Acceptance criteria

The P0 governance layer is complete when the first vertical slice can delegate one bounded harmless task while the Conversation Agent remains responsive; show the task, route, permissions, estimated and actual usage, and cancellation state; require confirmation under a simulated low-budget condition; deny an ungranted capability; reject a worker's unsupported success claim; recover safely after forced restart; and produce an inspectable, redacted audit trail. No worker may create another worker, modify protected state, exceed its reserved budget, or perform a consequential action without a valid task-bound approval.
