# Transparent Local Memory Vault

## Purpose and invariants

The memory vault is the canonical, user-owned record that gives Hey Computer continuity without preserving an endless hidden prompt. It is an ordinary Obsidian-compatible folder of UTF-8 Markdown and small JSON files; Obsidian is optional. Every durable claim has provenance, creation and update times, confidence, and a visible correction or deletion path. SQLite, embeddings, provider conversation IDs, and model caches are derived conveniences and may never become the only copy of user memory. The first release supports one local profile.

The system follows five invariants:

1. Raw audio is never written by default, and idle pre-wake audio exists only in a volatile rolling buffer.
2. Raw post-wake transcripts are temporary source material, not durable memory.
3. Worker output cannot directly become memory; only the Context Agent may propose changes, and policy-controlled vault operations commit them.
4. User edits outrank model-generated content and are never silently overwritten.
5. Deleting canonical content removes it from every derived index and future prompt after the next successful reconciliation.

## Vault location and directory tree

The default vault lives below the user's local application-data directory and can be moved through settings. The selected folder itself is portable and may be opened directly in Obsidian, copied, backed up, or version-controlled. Internal caches live under `.heycomputer/` so the user-facing notes remain clean.

```text
HeyComputer Vault/
├── README.md
├── Inbox/
│   └── Proposed Memories.md
├── Memory/
│   ├── Facts/
│   ├── Preferences/
│   ├── People/
│   └── Places/
├── Projects/
│   └── <project-slug>/
│       ├── overview.md
│       ├── decisions/
│       └── notes/
├── Tasks/
│   ├── Active/
│   └── Archive/
├── Timeline/
│   └── YYYY/
│       └── YYYY-MM-DD.md
├── Conversations/
│   └── Summaries/
│       └── YYYY/
│           └── YYYY-MM-DD/
├── Sources/
│   └── YYYY/
│       └── YYYY-MM-DD/
├── Trash/
├── Exports/
└── .heycomputer/
    ├── manifest.json
    ├── locks/
    ├── pending/
    ├── index/
    │   └── memory.db
    ├── revisions/
    └── recovery/
```

`Memory/`, `Projects/`, `Tasks/`, `Timeline/`, and `Conversations/Summaries/` are canonical. `Sources/` contains sanitized source receipts, never raw provider payloads or private reasoning. `Inbox/` holds uncertain proposals needing review. `Trash/` implements recoverable deletion with a configurable retention period. Everything under `.heycomputer/index/` is disposable and excluded from export by default.

## Common frontmatter schema

Every managed Markdown file begins with YAML frontmatter. Schema versions are integers and migration is explicit: readers must accept older supported versions, write only the current version, and preserve unknown fields during a round trip. IDs are stable opaque identifiers generated locally; filenames are descriptive labels and may change without breaking links.

```yaml
---
schema: heycomputer.memory/v1
id: mem_01JEXAMPLE
kind: fact # fact | preference | person | place | project | decision | task | event | conversation_summary
title: Preferred spoken reply length
status: active # proposed | active | disputed | superseded | deleted
created_at: 2026-10-23T19:12:00-07:00
updated_at: 2026-10-23T19:12:00-07:00
effective_at: 2026-10-23T19:10:00-07:00
confidence: 0.96
confidence_basis: explicit_user_statement
sensitivity: private # ordinary | private | restricted
retention: durable # session | temporary | durable | until_date
tags: [conversation-style, voice]
entities: []
projects: [hey-computer]
supersedes: []
superseded_by: []
sources:
  - source_id: src_01JEXAMPLE
    relation: supports # supports | contradicts | corrects | derives_from
    excerpt: "User explicitly requested brief speech-mode replies."
created_by: context-agent
last_modified_by: context-agent
user_locked: false
content_hash: sha256:<hash-of-normalized-body-and-managed-fields>
---
```

The body contains readable prose, not a serialized object. `confidence` expresses confidence that the record accurately reflects the source, not whether the source itself is objectively true. Explicit user statements normally outrank inference; contradictory records remain visible and linked until resolved. `sensitivity` controls provider disclosure. `user_locked: true` prevents automated modification until the user unlocks the note.

## Note-type schemas

Each note uses the common fields plus a small type-specific block. This keeps files pleasant to read while allowing deterministic validation and retrieval.

```yaml
# fact or preference
subject: user
predicate: prefers_spoken_reply_length
value: brief
valid_from: 2026-10-23
valid_until: null

# decision
decision: Use a three-runtime supervisory architecture
rationale: Keep conversation responsive while work and memory updates run asynchronously.
state: accepted # proposed | accepted | revisiting | retired

# project
project_state: active # active | paused | complete | archived
aliases: [Hey Computer]
related_tasks: [task_01JEXAMPLE]

# task
task_state: queued # queued | running | waiting | complete | failed | cancelled
owner: orchestration-agent
dependencies: []
result_refs: []

# event
occurred_at: 2026-10-23T19:10:00-07:00
participants: [user]

# conversation_summary
session_id: session_01JEXAMPLE
turn_range: [12, 18]
started_at: 2026-10-23T18:55:00-07:00
ended_at: 2026-10-23T19:12:00-07:00
summary_level: session
```

People notes must not infer sensitive traits, relationships, or identity from weak evidence. Task execution truth belongs in the Task Ledger; a task note is a readable projection and link target, not a competing state machine.

## Source receipts and provenance

A source receipt proves where a memory came from without retaining the complete transcript. Receipts use `schema: heycomputer.source/v1` and record a local source ID, source type, event time, originating session or task, creator, retention class, sensitivity, and a short sanitized excerpt or digest. For a conversation, the receipt contains only the minimum text needed to understand the claim; for a verified worker result, it contains the task ID, worker type, evidence references, and validation state. It must never contain credentials, raw audio, private reasoning, full provider requests, full screenshots, or hidden application data.

One source may support several notes, and one note may cite several sources. Corrections append a new `corrects` relationship rather than rewriting history invisibly. If a source receipt is deleted, dependent notes remain but become `provenance_missing: true`, lose retrieval priority, and appear in the review queue unless another source supports them.

## Transcript summarize-and-discard lifecycle

The transcript lifecycle is a bounded local transaction. Its default policy saves no durable transcript history and no audio.

1. **Capture:** After a valid wake or push-to-talk event, audio remains in volatile memory until transcription finishes or the turn is cancelled. A hard duration limit stops capture.
2. **Temporary transcript:** The transcription result is written atomically to an encrypted or access-controlled temporary file under `.heycomputer/pending/` only when crash recovery requires it. Otherwise it remains in memory. The default time-to-live is 15 minutes.
3. **Conversation use:** The Conversation Agent receives the current utterance and retrieved context. Provider disclosure is logged as metadata, not content.
4. **Context proposal:** After the turn completes, the Context Agent receives the transcript, assistant reply, verified task outcomes, and existing relevant notes. It emits a session summary, zero or more proposed note changes, provenance links, confidence, and sensitivity classifications.
5. **Policy review:** Deterministic validation rejects malformed schemas, secrets, unsupported inferences, duplicate claims, and direct worker writes. Low-confidence, contradictory, sensitive, or destructive changes go to `Inbox/Proposed Memories.md`; safe high-confidence changes may commit automatically.
6. **Atomic commit:** Accepted notes, source receipts, and the conversation summary are written through a journaled transaction. The index update occurs only after canonical files are durable.
7. **Discard:** Once commit succeeds, or once the retention deadline arrives, raw transcript text and any recovery copy are securely removed on a best-effort basis. The runtime releases in-memory copies. Only the summary and minimal source excerpts remain.
8. **Recovery:** On restart, expired pending files are deleted before models initialize. Unexpired files are either resumed locally or shown as recoverable items; they are never uploaded automatically.

If summarization fails, the app may retry within the temporary retention window without extending it indefinitely. At expiration it discards the transcript and records only a content-free operational failure. The user can choose immediate discard, a shorter window, or explicit transcript retention later, but durable raw transcripts are not the default.

## Corrections, disputes, and user authority

Manual edits are authoritative. When the user edits a managed note, the watcher validates it, records a local revision, sets `last_modified_by: user`, and preserves the edit even if fields are incomplete. A user correction creates a new source receipt with `source_type: user_correction`, lowers or disputes conflicting memories, and links them through `corrects`, `supersedes`, or `contradicts`. Automated maintenance may add links or propose consolidation, but it cannot change a user-locked note or restore text the user removed.

Conflicting claims are not averaged into a false certainty. Retrieval prefers explicit recent corrections, then explicit user statements, then verified outcomes, then model inference. A disputed fact is excluded from action authorization and presented as uncertainty when relevant. Superseded notes remain inspectable but are not injected into normal prompts.

## Deletion and retention

Deletion must be understandable, complete, and recoverable by default. Deleting a note changes its canonical status to `deleted`, removes its body from normal retrieval, writes a tombstone containing only the ID, deletion time, reason, and affected links, and moves the file into `Trash/`. The default trash retention is 30 days; “delete permanently” purges it immediately. Purge removes source excerpts that exist only for the deleted item, derived index rows, embedding vectors, previews, and recovery snapshots on the next cleanup pass.

References from other notes become explicit broken or deleted links rather than silently recreating the memory. A tombstone prevents sync, index rebuilds, or stale agent output from resurrecting deleted data. Backups outside Hey Computer are beyond the application's control, which the deletion UI must state plainly. Session and temporary memories expire automatically without entering durable history unless needed for an audit-safe tombstone.

## Export and portability

“Export vault” creates a standard archive of canonical Markdown, user-visible JSON manifests, attachments the user explicitly included, and a machine-readable relationship map. It excludes SQLite indexes, locks, caches, credentials, raw audio, temporary transcripts, provider payloads, and diagnostic logs. A privacy report lists each exported note's sensitivity and any broken provenance links. The archive must open as a useful folder in Obsidian without Hey Computer installed.

A separate redacted export omits `private` and `restricted` notes and rewrites links safely. Exports never upload automatically. Import validates schema versions, path traversal, file size, duplicate IDs, malicious links, and tombstones before modifying the live vault.

## Concurrent writes and manual-edit conflicts

All managed writes go through a single local `VaultWriter` queue. It takes a short per-note lock, compares the expected `content_hash`, writes a complete temporary file in the same directory, flushes it, and atomically replaces the target. A transaction journal groups multi-file changes so restart recovery either completes or rolls back the set. The filesystem watcher ignores changes bearing the writer's transaction ID, preventing feedback loops.

If the file hash changed since an agent read it, the agent write is rejected rather than merged blindly. Non-overlapping metadata additions may be three-way merged automatically; body edits, deletions, status changes, and overlapping frontmatter changes create a conflict copy under `.heycomputer/recovery/` and a review item in `Inbox/`. User text remains the live version. Parallel Context Agent runs may propose changes concurrently, but commits are serialized and revalidated against the newest vault state. Obsidian, text editors, backup tools, and sync clients are therefore supported without assuming exclusive control of the directory.

## Retrieval layers

Retrieval assembles the smallest relevant context and records why each item was selected. It never exposes hidden reasoning or injects the whole vault.

1. **Working context:** Current turn, recent turns held in memory, active task states, and pending user corrections. This layer is short-lived.
2. **Exact structured matches:** Explicitly linked project, person, task, date, tag, or entity IDs; accepted decisions and user-locked preferences rank highest.
3. **Lexical search:** SQLite FTS returns keyword and phrase matches from active canonical notes.
4. **Semantic search:** Optional local or selected-provider embeddings find conceptually related notes. Embeddings inherit note sensitivity and may not be sent to a cloud provider without disclosure and policy approval.
5. **Recency and relevance ranking:** Rank by directness, source authority, confidence, current status, project scope, recency, and repetition penalties. Do not let recency override a durable explicit preference without contrary evidence.
6. **Context budget:** Deduplicate, resolve superseded records, exclude deleted or disputed claims where unsafe, and stop at a configurable token budget.

Every returned item includes its memory ID, title, source class, confidence, sensitivity, and selection reason such as `active_project`, `explicit_preference`, or `semantic_match`. The UI should expose a “Why was this remembered?” view. Restricted memories require an explicit allow rule before leaving the device and never authorize an action on their own.

## Rebuildable SQLite index

`.heycomputer/index/memory.db` is a performance cache built solely from canonical files. Its initial schema contains `documents`, `frontmatter`, `links`, `sources`, `revisions`, and an FTS5 virtual table over title, body, tags, aliases, entities, and project labels. Optional embeddings live in a separate derived table keyed by memory ID, content hash, embedding model, and model version. The index stores no text absent from the vault.

On startup, the indexer compares the manifest generation and per-file content hashes. It incrementally reparses changed files, removes tombstoned entries, and quarantines malformed notes without hiding them from the user. A full rebuild deletes the database, scans canonical directories, validates schemas, resolves links, recreates FTS rows, and optionally regenerates embeddings. Search remains available in a slower direct-file mode while the index is unavailable. Database corruption must never damage or block access to canonical notes.

## Privacy boundaries

The vault runs under the signed-in user's permissions and is not shared across Windows accounts. Credentials remain in Windows Credential Manager and are referenced only by nonsecret IDs. The vault must never store wake-word buffers, raw audio by default, API keys, access tokens, cookies, private model reasoning, full provider payloads, or unrestricted screenshots. Operational logs may record memory IDs, timings, schema errors, and byte counts, but not note bodies or source excerpts.

Before sending retrieved memory to a cloud model, the runtime produces a disclosure plan identifying the provider and sensitivity classes involved. `ordinary` notes may follow the user's provider policy, `private` notes require an enabled provider-specific permission, and `restricted` notes remain local unless the user explicitly approves that disclosure for the task. Memory used for computer actions is input context, never permission; the Capability Broker remains the authority.

File-system encryption is an optional defense-in-depth setting, not a reason to make the vault opaque. If enabled, the product must explain its effect on Obsidian compatibility, backup, recovery, and search. Local malware and administrators can access user files, so “local-first” must not be marketed as absolute secrecy.

## Recovery and migrations

`.heycomputer/manifest.json` records the vault format, current schema versions, last clean transaction, index generation, and enabled features without conversational content. Before a schema migration, the app creates a local revision snapshot and dry-runs validation. Migrations are idempotent, preserve unknown fields, and never require network access. If migration fails, the prior files stay active and the vault opens read-only with a clear recovery action.

Unexpected shutdown recovery processes the journal before indexing. It removes orphan temporary files, completes only transactions whose full commit set is valid, restores earlier revisions for partial replacements, and emits a content-free audit event. Recovery must not call a model or extend transcript retention.

## P0 acceptance tests

The first implementation is complete only when these behaviors are automated where possible and exercised manually where editing software is involved:

- Create each note type, validate its frontmatter, preserve unknown fields, rename its file, and still resolve it by stable ID.
- Complete a conversation turn, produce a summary and supported memory, rebuild the index, restart the app, and retrieve the same memory with a visible selection reason.
- Verify pre-wake audio and raw audio never reach disk under default settings.
- Verify a temporary transcript disappears after successful summarization, cancellation, timeout, crash recovery, and failed summarization at retention expiry.
- Inject credentials, tokens, private reasoning, and full provider payloads into proposed changes and confirm policy rejects or redacts them before commit.
- Submit low-confidence, contradictory, sensitive, and destructive proposals and confirm they enter review rather than silently changing active memory.
- Edit a note in Obsidian while the Context Agent holds an older version; confirm the user's body wins and a conflict is surfaced without data loss.
- Run two Context Agent commits against the same note and confirm serialized validation prevents last-writer-wins corruption.
- Corrupt or delete `memory.db`, rebuild it offline from Markdown, and confirm search results and tombstone exclusions are restored.
- Delete, restore, and permanently purge a memory; confirm retrieval, FTS, embeddings, summaries, revisions, and dependent links follow the retention policy without resurrection.
- Correct a fact and confirm the correction outranks the old claim, retains provenance, and prevents the disputed value from authorizing an action.
- Export the full and redacted vaults; confirm they open without the application, contain no credentials or temporary transcripts, and differ according to sensitivity rules.
- Attempt path traversal, duplicate IDs, unsupported schema versions, oversized files, and malicious links during import; confirm the live vault remains unchanged.
- Deny cloud disclosure of private and restricted memory, then confirm retrieval can remain local and the remote request omits the protected material.
- Force a crash during a multi-file commit and confirm journal recovery produces either the complete old state or complete new state, never a mixed state.

## P0 implementation boundary

P0 includes one-user Markdown storage, the schemas above, temporary transcript handling, source receipts, Context Agent proposals, deterministic policy validation, serialized atomic writes, manual-edit detection, corrections, recoverable deletion, full and redacted export, FTS5 retrieval, index rebuild, disclosure filtering, and the acceptance tests. Vector search, vault encryption, retained raw transcripts, multi-device sync, multiple profiles, automatic knowledge-graph inference, and cloud backup remain later work. The P0 design should leave extension points for them without adding their operational risk now.
