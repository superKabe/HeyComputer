# Project Punch List

This is the durable, prioritized backlog. Keep it current by moving items between sections rather than duplicating them. Each completed architectural decision should be reflected in `AGENTS.md` and the relevant design document.

## P0 — Define the product core

- [x] Adopt the thesis: “An open-source, local-first Windows Jarvis that remembers transparently and delegates work to whichever agent is best.”
- [x] Define persistent roles: Conversation Agent, Orchestration Agent, Context Agent, and disposable Worker Agents.
- [x] Decide that local-first means local canonical data and control state; cloud inference is allowed.
- [x] Decide that multiple user or speaker memory profiles are deferred.
- [x] Decide that low-budget conditions always require asking before continued paid work.
- [ ] Specify typed contracts and message flow among the three persistent agents and workers.
- [ ] Define lifecycle and crash recovery so conversation, tasks, and memory updates survive restarts safely.
- [ ] Reconcile older architecture, privacy, onboarding, and threat-model documents with the new product direction.

## P0 — Transparent memory

- [ ] Design the Obsidian-compatible vault tree and versioned Markdown/metadata schemas.
- [ ] Separate recent context, facts, decisions, people, projects, active tasks, events, and source records.
- [ ] Define transcript retention: automatically capture post-wake text, summarize it, then discard raw transcripts on a documented schedule.
- [ ] Keep raw audio ephemeral and disabled for storage by default.
- [ ] Add provenance, timestamp, confidence, correction, deletion, export, and reset behavior.
- [ ] Build a disposable SQLite full-text index; evaluate embeddings only after plain retrieval works.
- [ ] Define conflict handling when the user manually edits memory while the Context Agent is running.
- [ ] Add memory-quality tests for stale facts, contradictions, over-retention, and irrelevant retrieval.

## P0 — Orchestration, budgets, and permissions

- [ ] Design the task ledger: objective, owner, worker, dependencies, status, evidence, deadline, cancellation, and result.
- [ ] Design the usage ledger with actual provider usage where available and clearly labeled estimates otherwise.
- [ ] Define model-routing policy by capability, quality, latency, privacy, and cost.
- [ ] Cap concurrent workers and prevent recursive or duplicate fan-out.
- [ ] Ask before continuing when budgets are low or proposed work exceeds configured thresholds.
- [ ] Create the worker capability model and a detailed permission/confirmation matrix.
- [ ] Prevent workers from directly changing personality, durable memory, permissions, or budgets.
- [ ] Define recovery for worker timeout, partial success, conflicting edits, provider failure, and unverifiable completion.

## P0 — First vertical slice

- [ ] Complete: wake → listen → immediate acknowledgement → understand → delegate one harmless task.
- [ ] Keep the Conversation Agent responsive while the worker runs.
- [ ] Have the Context Agent summarize the exchange into the inspectable vault in parallel.
- [ ] Return a verified worker result through the stable conversational personality.
- [ ] Show active task, selected model, estimated usage, permissions, and cancellation control.
- [ ] Restart the application mid-task and verify safe recovery without duplicate paid work.

## P1 — Validate the existing voice foundation

- [ ] Finish GitHub authentication, create the initial commit, and publish the repository under the chosen visibility.
- [ ] Configure a user-supplied xAI credential through the application UI.
- [ ] Resolve a legally redistributable English “Hey Computer” wake model.
- [ ] Run guided microphone calibration in quiet, music, television, and distance scenarios.
- [ ] Complete and measure a live end-to-end spoken exchange.
- [ ] Add barge-in and echo/self-trigger protection after the baseline loop is reliable.
- [ ] Validate sleep/resume, device changes, Bluetooth behavior, mute, cancellation, and startup registration.

## P1 — Safe computer control

- [ ] Inventory reusable runtimes and licenses: Windows UI Automation/COM/Win32, browser automation, computer-use models, MCP tools, and specialist coding agents.
- [ ] Define the capability broker and local action audit format before enabling writes.
- [ ] Begin with read-only screen, window, accessibility-tree, filesystem, and browser observation.
- [ ] Add reversible local actions with narrow per-application scopes.
- [ ] Prefer direct APIs and structured controls; use vision-driven clicking only as a fallback.
- [ ] Require preview or confirmation for messages, deletion, purchases, uploads, credentials, security changes, and other consequential actions.
- [ ] Build replayable evaluations for common Windows workflows and recovery from stale windows, focus errors, popups, and unexpected UI changes.

## P1 — Open-source foundation

- [ ] Choose the project license and document third-party license obligations.
- [ ] Define the boundary between open core and any optional hosted services.
- [ ] Publish architecture, threat model, memory format, provider interface, and contribution guide.
- [ ] Provide a plugin or worker adapter SDK with scoped capabilities and versioned contracts.
- [ ] Create deterministic tests and public reliability benchmarks rather than capability-only demos.
- [ ] Add secret scanning, dependency review, release provenance, and a responsible disclosure policy.

## P2 — Productization

- [ ] Rework onboarding around memory, provider data flow, computer permissions, and budget controls.
- [ ] Provide a signed per-user installer, clean uninstall, safe updates, and rollback.
- [ ] Add a task/memory/usage dashboard without making the conversational experience feel like project-management software.
- [ ] Support provider switching without losing identity, memory, or task history.
- [ ] Add sanitized diagnostics and user-exportable support bundles.
- [ ] Measure idle CPU, memory, battery, wake accuracy, response latency, task success, and cost.

## P3 — Later possibilities

- [ ] Optional local inference for routing, classification, summarization, retrieval, and other light work.
- [ ] Multiple user or speaker memory profiles.
- [ ] User-defined wake phrases.
- [ ] Fully offline speech recognition and synthesis.
- [ ] Broader unattended workflows, schedules, smart-home integrations, and multi-device workers.
- [ ] Managed usage plans in addition to bring-your-own-provider credentials.
