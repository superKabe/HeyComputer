# Agent Contracts and Message Flow

## Purpose

This specification defines the P0 boundary between the Conversation Agent, Orchestration Agent, Context Agent, and task-scoped Worker Agents. It is the implementation contract for the first architectural vertical slice: keep conversation responsive, delegate one harmless task, update local memory in parallel, survive cancellation or restart, and return one verified result through the stable conversational personality. The protocol is designed for an in-process C#/.NET application today without preventing durable queues or out-of-process workers later.

## Design rules

The Conversation Agent is the only component that speaks to the user and the only component allowed to apply the personality profile. The Orchestration Agent exclusively owns task planning, worker assignment, budget checks, and completion verification. The Context Agent exclusively proposes durable memory changes from completed turns and verified task outcomes. Workers perform bounded assignments but cannot speak to the user, spawn other workers, change budgets or permissions, or write canonical memory. All cross-runtime communication uses immutable typed messages rather than direct calls into another runtime's internal state.

Commands request an action from one named owner and may be accepted or rejected. Events state an immutable fact that already occurred and may have multiple consumers. A command must not be treated as proof that its requested action happened; consumers act only on the corresponding event. Canonical state lives in the local ledgers and memory vault, not in an agent prompt or provider-owned conversation.

## Runtime identities

Persistent roles use stable logical identities across process restarts. Worker identities are unique to one assignment attempt. A provider or model is execution metadata, not agent identity, so changing models does not create a new Conversation Agent or alter its personality.

```csharp
public enum AgentRole
{
    Conversation,
    Orchestration,
    Context,
    Worker,
    System
}

public readonly record struct AgentId(string Value)
{
    public static AgentId Conversation => new("conversation");
    public static AgentId Orchestration => new("orchestration");
    public static AgentId Context => new("context");
    public static AgentId System => new("system");

    public static AgentId Worker(TaskId taskId, int attempt) =>
        new($"worker:{taskId.Value}:{attempt}");
}
```

The initial product has one local user profile, so messages do not yet contain a user or speaker identifier. Adding multiple profiles later requires a versioned contract change rather than overloading `AgentId`.

## Identifiers and correlation

Identifiers are opaque strings serialized in lowercase canonical form. The implementation should generate time-sortable UUID version 7 values with `Guid.CreateVersion7()` where the target .NET runtime supports it; otherwise use random GUIDs. Code must compare identifiers for equality only and must not derive authorization or ordering from their contents.

```csharp
public readonly record struct MessageId(string Value);
public readonly record struct SessionId(string Value);
public readonly record struct TurnId(string Value);
public readonly record struct TaskId(string Value);
public readonly record struct AttemptId(string Value);
public readonly record struct OperationId(string Value);
public readonly record struct MemoryProposalId(string Value);
```

- `SessionId` groups conversation after an application start or explicit new-session boundary. It is useful context, not a durability boundary.
- `TurnId` begins when a post-wake utterance is accepted and remains unchanged through acknowledgement, delegation, context processing, and final narration.
- `TaskId` identifies one durable unit of requested work and remains stable across retries or reassignment.
- `AttemptId` identifies one execution attempt for a task. A retry receives a new attempt while retaining the same task.
- `OperationId` identifies a capability-broker action such as one filesystem read. Replays use the same operation identifier.
- `MessageId` uniquely identifies one immutable envelope and is the idempotency key for message delivery.
- `CausationId` points to the message that directly caused this message. `CorrelationId` is normally the originating `TurnId`; system recovery work may instead correlate to a `TaskId`.

## Message envelope

Every command and event uses the same versioned envelope. Payloads are immutable records and are serialized with `System.Text.Json`. Envelopes are persisted before delivery when they affect tasks, usage, memory, permissions, or user-visible completion.

```csharp
public enum MessageKind { Command, Event }

public sealed record AgentMessage<TPayload>(
    MessageId MessageId,
    int SchemaVersion,
    MessageKind Kind,
    string MessageType,
    DateTimeOffset OccurredAtUtc,
    AgentId Sender,
    AgentId Recipient,
    string CorrelationId,
    MessageId? CausationId,
    TurnId? TurnId,
    TaskId? TaskId,
    AttemptId? AttemptId,
    int DeliveryAttempt,
    TPayload Payload);
```

`OccurredAtUtc` records when the producer made the fact or request; it is not used to establish ordering. `DeliveryAttempt` is transport metadata and may increase while `MessageId` remains unchanged. Payload schemas start at version 1. Readers must ignore unknown JSON properties, reject unsupported major schema versions without side effects, and emit `MessageRejected` for diagnosable incompatibility.

```csharp
public sealed record MessageRejected(
    MessageId RejectedMessageId,
    string SafeReason,
    bool MayRetry);
```

When one event has multiple consumers, the journal creates one envelope per recipient with a unique `MessageId`, the same correlation fields and payload, and a shared `CausationId` pointing to the producing message. Consumers never infer broadcast delivery from a single recipient envelope.

## Common value contracts

These shared records keep provider-specific objects, secrets, and unrestricted tool handles out of the protocol. Text references point to locally protected records where practical so ledgers do not duplicate full transcripts or sensitive results.

```csharp
public sealed record ContentReference(
    string Scheme,       // e.g. "vault", "transient", "task-result"
    string Value,
    string Sha256);

public sealed record ModelSelection(
    string ProviderId,
    string ModelId,
    string SelectionReason,
    bool IsLocal);

public sealed record UsageBudget(
    long? MaximumInputTokens,
    long? MaximumOutputTokens,
    decimal? MaximumEstimatedCost,
    DateTimeOffset? DeadlineUtc);

public sealed record UsageMeasurement(
    long? InputTokens,
    long? OutputTokens,
    decimal? Cost,
    bool IsEstimate,
    string Currency);

public sealed record CapabilityGrant(
    string Capability,
    string Scope,
    bool RequiresConfirmation);

public sealed record EvidenceReference(
    string Kind,
    ContentReference Content,
    string Description);

public enum FailureCategory
{
    Cancelled,
    TimedOut,
    PermissionDenied,
    BudgetExceeded,
    ProviderUnavailable,
    InvalidRequest,
    InvalidResult,
    Conflict,
    Internal
}

public sealed record FailureInfo(
    FailureCategory Category,
    string SafeSummary,
    bool IsRetryable,
    string? DiagnosticCode = null);
```

`SafeSummary` must be suitable for sanitized local logs and must not contain credentials, transcript text, raw provider payloads, or private reasoning. A credential is represented only by a provider-owned credential reference outside these messages.

## Conversation contracts

The Conversation Runtime owns the live turn and may acknowledge immediately without waiting for orchestration or context work. It publishes accepted content once transcription is final, receives status and terminal task facts, and decides what should be spoken. It does not infer successful work from silence, a dispatched command, or an unverified worker response.

```csharp
public sealed record TurnAccepted(
    ContentReference Transcript,
    string InputModality,
    bool UserRequestedAction);

public sealed record ImmediateAcknowledgementIssued(string AcknowledgementKind);

public sealed record ConversationResponseIssued(
    string ResponsePurpose, // acknowledgement, clarification, status, result, failure
    ContentReference ResponseText,
    bool WasSpoken);

public sealed record ResultPresentationClaimed(TaskId TaskId);
public sealed record CancelTurn(TurnId TargetTurnId, string Reason);
public sealed record TurnCancelled(TurnId TargetTurnId, string Reason);
public sealed record TurnCompleted(string Outcome); // answered, delegated, cancelled, failed
```

`TurnAccepted` is consumed by Orchestration and may be observed transiently by Context, but Context does not propose durable memory from an incomplete turn. `TurnCompleted` means the interactive exchange reached a conversational boundary and makes the turn eligible for context processing; it does not imply that every delegated task has finished. A task may outlive its originating turn and later produce a result for a new conversational response. Before narrating a recovered task result, Conversation durably emits `ResultPresentationClaimed`; a claimed result is not automatically spoken again after restart, avoiding duplicate speech at the cost of a narrow crash window in which the user must ask for task status.

## Orchestration contracts

The Orchestration Runtime converts actionable intent into a durable task plan. For the first vertical slice it creates no more than one worker task per turn. Later fan-out still requires explicit dependency records, a concurrency cap, and budget evaluation before assignment.

```csharp
public sealed record AnalyzeTurnForWork(
    TurnId SourceTurnId,
    ContentReference Transcript,
    ContentReference? RetrievedContext);

public sealed record TaskPlanned(
    string Objective,
    IReadOnlyList<TaskId> Dependencies,
    IReadOnlyList<CapabilityGrant> Capabilities,
    UsageBudget Budget,
    string ExpectedResultSchema,
    DateTimeOffset CreatedAtUtc);

public sealed record StartTaskAttempt(
    AttemptId AttemptId,
    string WorkerKind,
    ModelSelection Model,
    string Objective,
    IReadOnlyList<CapabilityGrant> Capabilities,
    UsageBudget Budget,
    string ExpectedResultSchema);

public sealed record TaskAttemptStarted(string WorkerKind, ModelSelection Model);
public sealed record TaskProgressed(int? Percent, string SafeSummary);

public sealed record TaskAttemptSucceeded(
    ContentReference Result,
    IReadOnlyList<EvidenceReference> Evidence,
    UsageMeasurement Usage);

public sealed record TaskAttemptFailed(
    FailureInfo Failure,
    UsageMeasurement Usage);

public sealed record TaskAttemptInterrupted(
    FailureInfo Failure,
    UsageMeasurement Usage);

public sealed record TaskRetryScheduled(
    AttemptId PreviousAttemptId,
    AttemptId NextAttemptId,
    string SafeReason);

public sealed record TaskReviewRequired(
    AttemptId AttemptId,
    FailureInfo Failure);

public sealed record VerifyTaskResult(
    AttemptId AttemptId,
    ContentReference Result,
    IReadOnlyList<EvidenceReference> Evidence);

public sealed record TaskResultVerified(
    AttemptId AttemptId,
    ContentReference Result,
    IReadOnlyList<EvidenceReference> Evidence,
    UsageMeasurement TotalUsage);

public sealed record TaskResultRejected(AttemptId AttemptId, FailureInfo Failure);
public sealed record CancelTask(TaskId TargetTaskId, string Reason);
public sealed record TaskCancellationRequested(string Reason);
public sealed record TaskCancelled(string Reason, UsageMeasurement Usage);
```

Only `TaskResultVerified` is a successful terminal result delivered to Conversation and Context. `TaskAttemptSucceeded` means a worker returned output, not that the objective was met. For the harmless first task, verification may be deterministic schema and evidence validation inside Orchestration; the contract leaves room for a separate verifier later without changing consumers.

## Budget and confirmation contracts

Budget decisions occur before paid work starts and again before any retry that can incur additional usage. When a configured budget is low or a proposed operation would cross a threshold, orchestration pauses rather than silently reducing quality, switching policy, or continuing.

```csharp
public sealed record BudgetEvaluated(
    TaskId TaskId,
    UsageBudget Requested,
    UsageMeasurement CurrentPeriodUsage,
    bool MayProceed,
    bool RequiresUserConfirmation,
    string SafeReason);

public sealed record RequestBudgetConfirmation(
    TaskId TaskId,
    UsageBudget Requested,
    ModelSelection ProposedModel,
    string SafeReason);

public sealed record ResolveBudgetConfirmation(
    TaskId TaskId,
    bool Approved,
    UsageBudget? ApprovedBudget);

public sealed record BudgetConfirmationResolved(bool Approved, UsageBudget? ApprovedBudget);
```

The Conversation Agent presents the confirmation and sends the user's decision. Orchestration remains the owner of the paused task. A denial ends or replans the task without paid execution. Confirmation messages never carry payment credentials or provider secrets.

## Context and memory contracts

The Context Runtime observes completed conversational facts and verified task outcomes. It may create a proposed change in parallel with worker execution, but it must not turn an unverified worker result into durable fact. Canonical memory changes pass through a memory policy and an atomic vault commit.

```csharp
public sealed record ObserveTurn(
    TurnId TurnId,
    ContentReference Transcript,
    IReadOnlyList<ContentReference> AssistantResponses,
    string Outcome);

public sealed record ObserveVerifiedTaskResult(
    TaskId TaskId,
    TurnId? SourceTurnId,
    ContentReference Result,
    IReadOnlyList<EvidenceReference> Evidence);

public sealed record ProposeMemoryChange(
    MemoryProposalId ProposalId,
    string ChangeKind, // create, amend, supersede, forget
    string TargetPath,
    ContentReference ProposedContent,
    IReadOnlyList<ContentReference> Sources,
    double Confidence,
    DateTimeOffset ObservedAtUtc);

public sealed record MemoryProposalAccepted(
    MemoryProposalId ProposalId,
    string TargetPath,
    string ExpectedBaseHash);

public sealed record MemoryProposalRejected(
    MemoryProposalId ProposalId,
    string SafeReason);

public sealed record MemoryChangeCommitted(
    MemoryProposalId ProposalId,
    string TargetPath,
    string NewContentHash);

public sealed record MemoryChangeConflicted(
    MemoryProposalId ProposalId,
    string TargetPath,
    string CurrentContentHash);
```

The Context Agent must preserve provenance and uncertainty in its proposal. Memory policy may auto-accept low-risk summaries under the configured policy, but uncertain identity, preference, or factual changes remain proposals for review. The vault writer uses compare-and-swap with `ExpectedBaseHash`; a user edit wins over a stale automated proposal, which becomes `MemoryChangeConflicted` and is never overwritten silently.

## Worker contract

A worker receives exactly one `StartTaskAttempt`, a bounded context package, capability grants, a model selection, a budget, a deadline, and an expected result schema. It reports progress sparingly and finishes with exactly one terminal attempt event. It cannot modify task state directly; the ledger projects state from accepted events.

A worker may call the Capability Broker only with its own `TaskId`, `AttemptId`, and an explicitly granted capability. Each requested action carries a stable `OperationId`. The broker rejects missing, expired, or over-broad grants and records an audit event. The first vertical slice should use a harmless read-only or pure computation worker so no user confirmation is required.

```csharp
public sealed record ExecuteCapability(
    OperationId OperationId,
    string Capability,
    string ScopedTarget,
    ContentReference Parameters);

public sealed record CapabilityExecutionSucceeded(
    OperationId OperationId,
    ContentReference Result,
    EvidenceReference Evidence);

public sealed record CapabilityExecutionFailed(
    OperationId OperationId,
    FailureInfo Failure);
```

Worker prompts and provider requests are derived execution details. They are not protocol messages, are not canonical task state, and must never contain capabilities beyond the grants in `StartTaskAttempt`.

## Message ownership and consumers

Each command has one authoritative recipient. Events may be projected into more than one local store, but only the listed owner may produce the authoritative state transition.

| Message family | Producer | Primary consumer | Durable before delivery |
| --- | --- | --- | --- |
| Turn events | Conversation | Orchestration and Context | Yes, except ephemeral acknowledgement playback |
| Task commands and attempt events | Orchestration or assigned Worker | Named recipient; Task Ledger projects events | Yes |
| Verified task result | Orchestration | Conversation and Context | Yes |
| Memory proposals | Context | Memory policy/vault writer | Yes |
| Memory commit events | Vault writer | Context and audit projection | Yes |
| Budget confirmation request | Orchestration | Conversation | Yes |
| User confirmation resolution | Conversation | Orchestration | Yes |
| Capability command and result | Worker and Capability Broker | Named recipient; audit projection | Yes |
| Progress event | Worker | Orchestration, optionally Conversation | No; latest value may be transient |

An in-process `Channel<T>` may provide live delivery, but it is not the durable source. Durable messages use an append-only local message journal or an outbox row written in the same SQLite transaction as the relevant ledger change. A dispatcher reads undispatched rows and delivers them to runtime inboxes.

## Task lifecycle

The Task Ledger enforces this state machine. State changes occur only when the corresponding durable event is accepted.

```text
Proposed
  ├─> AwaitingBudgetConfirmation ─> Ready
  ├─> Ready ─> Assigned ─> Running ─> Verifying ─> Succeeded
  │                         │            └─> Failed
  │                         ├─> Ready (retry scheduled)
  │                         ├─> NeedsReview
  │                         ├─> Failed
  │                         └─> CancellationRequested ─> Cancelled
  ├─> Cancelled
  └─> Failed
```

- `Proposed`: `TaskPlanned` exists, but no paid or capability-bearing work has begun.
- `AwaitingBudgetConfirmation`: work is durably paused until the Conversation Agent returns a user decision.
- `Ready`: dependencies, permissions, and budget checks passed.
- `Assigned`: an attempt and worker identity were recorded; delivery may still be pending.
- `Running`: the worker accepted the assignment and emitted `TaskAttemptStarted`.
- `Verifying`: the worker returned `TaskAttemptSucceeded`; output is not yet trusted as complete.
- `NeedsReview`: an interrupted attempt may have incurred cost or external effects that cannot be established safely; no automatic retry is allowed.
- `Succeeded`: Orchestration emitted `TaskResultVerified`.
- `Failed` and `Cancelled`: terminal states. A retryable attempt failure may emit `TaskRetryScheduled` and move the task back to `Ready` with a new attempt only when policy, idempotency, and remaining budget permit it. Once the task itself reaches a terminal state, trying again requires an explicit new task linked to the original.

The ledger rejects invalid transitions, records the rejected message, and emits `MessageRejected` without mutating task state. `Succeeded`, `Failed`, and `Cancelled` are immutable terminal states.

## Cancellation

Cancellation is cooperative first and forceful at the runtime boundary after a timeout. A user cancel makes Conversation send `CancelTurn` to Orchestration. Orchestration finds each linked nonterminal task and issues `CancelTask`, then records `TaskCancellationRequested` before signaling the worker's `CancellationTokenSource`. Workers stop starting new capability operations, cancel provider streaming where supported, and emit `TaskCancelled` with usage already incurred. The Capability Broker rejects new operations after cancellation and attempts to cancel in-flight reversible operations.

Cancellation does not erase completed side effects, usage, audit history, or already committed memory. The Conversation Agent says what stopped and what may already have happened. If a worker process does not stop within the configured grace period, its host is terminated and recovery records the attempt as failed or cancelled based on the durable cancellation request.

## Idempotency and delivery semantics

Delivery is at least once, so every consumer must be idempotent. Before applying a durable message, the consumer checks its inbox receipt table using `(Recipient, MessageId)`. It performs the state change and records the receipt in one local transaction. A repeated envelope returns the previously recorded outcome without repeating provider calls, capability actions, vault writes, speech, or usage charges.

Commands that could create external side effects also use their domain identifier as an idempotency key: `AttemptId` for provider work, `OperationId` for capability actions, and `MemoryProposalId` plus expected hash for vault writes. If a provider supports idempotency keys, pass the stable identifier through. If it does not, never automatically retry an ambiguous request after losing the response; mark the attempt `NeedsReview` internally and ask or verify before incurring the action again. The first vertical slice avoids non-idempotent external side effects.

Ordering is guaranteed only within one task ledger transaction and its outbox. Consumers tolerate delayed events from older attempts by comparing `AttemptId` with the task's active attempt. Late progress or success from a superseded or cancelled attempt is retained for audit but cannot change the terminal task state.

## Crash recovery

At startup, the application opens the ledgers and journal, validates schema versions, and acquires the single-instance lock before starting any runtime. Recovery rebuilds projections from durable records where necessary, then resumes undispatched outbox messages. Conversation returns to a visible local waiting state independently of task recovery so the user can speak while recovery runs.

Recovery applies these rules:

1. `Proposed`, `AwaitingBudgetConfirmation`, and `Ready` tasks remain paused or ready without spending money.
2. `Assigned` tasks whose start was never acknowledged are redelivered with the same `AttemptId`.
3. In-process workers previously marked `Running` no longer exist after restart. Emit `TaskAttemptInterrupted`. Retry automatically with `TaskRetryScheduled` only when the assignment and every capability operation are demonstrably idempotent and the remaining budget permits it; otherwise emit `TaskReviewRequired` and move the task to `NeedsReview`.
4. `Verifying` tasks rerun local verification against the already stored result and evidence. They do not rerun the paid worker.
5. Terminal tasks are never restarted. Undelivered `TaskResultVerified` events are replayed so Conversation and Context can catch up idempotently.
6. Accepted but uncommitted memory proposals are retried against their recorded base hash. A changed file produces a conflict instead of overwrite.
7. A durable cancellation request wins over any recovered start command. Recovery completes cancellation before considering retry.
8. Usage reservations that lack a terminal measurement are retained as estimated usage until reconciled; they are never reset to zero merely because the process crashed.

Recovery writes a sanitized audit summary but does not restore raw microphone audio, partial speech playback, or private model reasoning. An interrupted conversational turn closes as interrupted; any durable task linked to it remains independently recoverable.

## First vertical-slice sequence

The first slice uses one harmless worker, such as summarizing a local non-sensitive sample document or computing a deterministic answer without write capabilities. The Conversation Agent must remain able to accept a second turn after step 3 while the rest continues.

```text
User -> Conversation: post-wake request
Conversation -> Journal: TurnAccepted(turn-1, transcript-ref)
Conversation -> User: immediate local acknowledgement

Journal -> Orchestration: AnalyzeTurnForWork(turn-1)
Orchestration -> Task Ledger: TaskPlanned(task-1, bounded objective, grants, budget)
Orchestration -> Usage Ledger: BudgetEvaluated(task-1, may proceed)
Orchestration -> Worker: StartTaskAttempt(task-1, attempt-1)
Worker -> Task Ledger: TaskAttemptStarted(attempt-1)

Conversation -> Journal: TurnCompleted(turn-1, delegated)
Journal -> Context: ObserveTurn(turn-1, transcript and acknowledgement)
Context -> Memory Policy: ProposeMemoryChange(proposal-1, turn summary)
Memory Policy -> Vault: MemoryProposalAccepted(proposal-1, expected base hash)
Vault -> Journal: MemoryChangeCommitted(proposal-1)

Worker -> Orchestration: TaskAttemptSucceeded(result-ref, evidence, usage)
Orchestration -> Orchestration: VerifyTaskResult(attempt-1)
Orchestration -> Journal: TaskResultVerified(task-1, result-ref, evidence, usage)
Journal -> Context: ObserveVerifiedTaskResult(task-1)
Journal -> Conversation: TaskResultVerified(task-1)
Conversation -> Journal: ResultPresentationClaimed(task-1)
Conversation -> User: concise personality-consistent result
Conversation -> Journal: ConversationResponseIssued(result)
```

If cancellation occurs before worker completion, the task follows `CancellationRequested` to `Cancelled`, Context may retain only the conversational summary permitted by policy, and Conversation reports cancellation without inventing a result. If the app crashes after `TaskAttemptSucceeded` but before verification, recovery resumes at verification using the saved result. If it crashes after verification but before presentation is claimed, the verified event is replayed and may be narrated. If it crashes after the claim, it does not narrate automatically again; the verified result remains visible and can be requested by the user.

## Runtime interfaces

The first implementation can expose small asynchronous ports while preserving the envelope boundary. Handlers must return only after their durable state and outbox changes are committed, not after downstream work completes.

```csharp
public interface IAgentMessageBus
{
    ValueTask PublishAsync<TPayload>(
        AgentMessage<TPayload> message,
        CancellationToken cancellationToken = default);
}

public interface IAgentMessageHandler<TPayload>
{
    ValueTask HandleAsync(
        AgentMessage<TPayload> message,
        CancellationToken cancellationToken);
}

public interface IAgentInbox
{
    IAsyncEnumerable<AgentMessageEnvelope> ReadAllAsync(
        AgentId recipient,
        CancellationToken cancellationToken);
}

public sealed record AgentMessageEnvelope(
    MessageId MessageId,
    int SchemaVersion,
    MessageKind Kind,
    string MessageType,
    DateTimeOffset OccurredAtUtc,
    AgentId Sender,
    AgentId Recipient,
    string CorrelationId,
    MessageId? CausationId,
    TurnId? TurnId,
    TaskId? TaskId,
    AttemptId? AttemptId,
    int DeliveryAttempt,
    JsonElement Payload);
```

Use explicit registration from `MessageType` to a payload type; never enable polymorphic type-name deserialization from untrusted JSON. Handler cancellation tokens control local shutdown or delivery work, while domain cancellation remains an explicit `CancelTurn` or `CancelTask` message so it is durable and recoverable.

## Validation requirements

The contract implementation is complete enough for the first vertical slice when automated tests prove: duplicate delivery causes one side effect; invalid lifecycle transitions are rejected; a late result cannot revive a cancelled task; a low-budget task does not start before approval; conversation accepts another turn while a worker runs; only verified results reach final narration and durable task memory; a manual vault edit causes a conflict rather than overwrite; cancellation blocks new capability operations; and restart at every durable boundary resumes without duplicate paid work, duplicate speech, or lost usage accounting.

## Deferred extensions

Multiple user identities, distributed workers, recursive task graphs, unrestricted computer control, cross-device synchronization, and local model hosting are outside this contract's first implementation. They should extend the versioned envelope and capability model rather than bypassing them. Provider adapters, coding agents, browser agents, and Windows automation runtimes remain replaceable workers; none becomes the canonical owner of personality, memory, permissions, task state, or budget state.
