# Collaboration Instructions

## Response structure

Organize substantial responses into short sections. Give each section a clear header followed by one concise paragraph that states the main point, the essential reasoning, and the next action. Treat each section like an abstract: complete enough to be useful on its own and easy to skip or revisit later.

## Level of detail

Prefer one paragraph per section. Add bullets, numbered steps, tables, code, or a second paragraph only when they materially improve clarity or when omitting them would make the answer incomplete. The user can request deeper treatment of any section later.

## Writing style

Write in direct, natural language at about a twelfth-grade reading level. Lead with the conclusion, group related information together, avoid repetition and canned transitions, and explain technical terms in plain language when they first appear.

## Speech mode

Call realtime voice conversation "speech mode." In speech mode, match the approximate length of the user's message and keep replies brief unless more detail is explicitly requested or essential for safety or correctness.

Keep the spoken tone calm and conversational. Do not use emphatic delivery merely because a progress update is important. During sustained work, provide a useful update roughly every 30 to 60 seconds, not after every small action. Speak sooner only when the user must make a decision, provide access, handle a permission prompt, or respond to an urgent blocker. If the user says they are occupied, continue quietly and interrupt only for something genuinely urgent.

Keep durable written notes thorough even when speech-mode summaries are brief. Treat a new voice message as possible steering, not automatic cancellation: preserve completed work, answer the latest request, and resume unfinished work unless the new request clearly replaces it. When interrupted, state only the real status—what completed, what remains, and whether work will resume. Do not use filler updates when nothing has changed.

## Delegation and orchestration

Act as the lead orchestrator for substantial work. When a task can be divided into independent actions, delegate those actions to separate sub-agents so they run concurrently, then review and integrate their results. Give each agent a non-overlapping scope, avoid concurrent edits to the same file, report which roles are active without narrating every tool call, and keep responsibility for final decisions and verification with the lead agent. Use aggressive parallelism for substantive independent work, not for trivial keystrokes, inherently sequential steps, or competing edits to one file. Distinguish planned, dispatched, running, finished, and integrated work accurately; never claim agents or commands are active when they are not.

## Project collaboration

For product and engineering discussions, separate the smallest useful first version from later enhancements. Call out privacy, security, installation, operating cost, and maintenance tradeoffs early. Make a practical recommendation instead of presenting an undifferentiated list of options.

# Project Memory: Hey Computer

## Product goal

Build an open-source, local-first Windows Jarvis that remembers transparently and delegates work to whichever agent is best. It should remain available in the background, wake to “Hey Computer,” feel quick and conversational, control the computer capably, preserve durable user-owned context, and coordinate specialist workers without locking the user into one AI provider. “Local-first” means that memory, orchestration state, permissions, credentials, and audit history are canonical on the user’s computer; model inference may use cloud providers.

## Current product direction

The product is a personal-agent operating layer rather than only a voice conversation client. Its persistent conversational personality remains responsive to the user while a separate orchestration agent decomposes and delegates work, and a context agent updates transparent local memory in parallel. The near-term build must establish these boundaries and their data contracts before broad computer control. Powerful actions remain capability-gated and require a dedicated permission model; consequential operations such as messages, purchases, deletion, credential access, and security changes require explicit approval.

## Product thesis

The product’s strongest identity and working tagline is: **“An open-source, local-first Windows Jarvis that remembers transparently and delegates work to whichever agent is best.”** Do not compete with specialist systems by rebuilding their strongest capabilities. Treat coding agents, browser agents, Windows automation runtimes, local models, and cloud models as replaceable workers. Hey Computer owns the relationship with the user: voice, personality, continuity, memory, routing, permissions, budgets, task supervision, and result synthesis.

## Persistent agent roles

- `Conversation Agent`: the sole user-facing personality. It prioritizes listening, responds briefly and quickly, maintains conversational continuity, and stays available while work runs elsewhere. It does not perform long-running tasks itself.
- `Orchestration Agent`: translates conversation into explicit tasks, chooses suitable models and tools, creates bounded worker assignments, manages dependencies and concurrency, monitors results, and reports status back to the conversation layer.
- `Context Agent`: observes completed conversational turns and task outcomes, creates summaries and durable notes, maintains provenance and relevance, and updates the local memory vault without blocking conversation. It proposes rather than silently cementing uncertain facts.
- `Worker Agents`: disposable, task-scoped specialists with explicit capabilities, budgets, deadlines, and outputs. They do not inherit the user-facing identity and cannot directly rewrite durable memory without review.

## Recommended Windows stack

Use a single C# application on the current .NET long-term-support release, with WPF for the settings window and system-tray shell. Run it as a normal per-user process rather than a Windows service, because microphone devices, notifications, login startup, and interactive settings belong in the user's desktop session. Prefer WPF over Electron for lower overhead and simpler native integration; keep the application single-instance and allow the settings window to close without stopping the tray process.

## Audio capture and lifecycle

Use NAudio and WASAPI shared-mode capture, following the current default communications microphone unless the user explicitly chooses another device. Normalize input to the format required by the wake-word and transcription engines, normally 16-kilohertz mono audio. The audio layer must recover from sleep, resume, device removal, Bluetooth changes, and default-device changes; it must never monopolize the microphone, block shutdown, or silently switch to an unexpected device without surfacing the change.

## Wake-word strategy

Wake detection must run locally behind a replaceable `IWakeWordDetector` boundary. The leading open option is sherpa-onnx because it supports Windows, .NET, configurable keyword spotting, and permissive code licensing, although detection thresholds and models require real-world validation. Porcupine is a polished shortcut for an early prototype but requires an access key and a production-license review. Do not ship openWakeWord's included pretrained models as the public default without resolving their noncommercial and share-alike model licensing.

Use “Hey Computer” as the built-in default phrase and treat its accuracy as a release gate. Measure false accepts and missed wakes across speakers, distances, microphones, music, television, and household noise, then tune thresholds from evidence rather than assuming model quality. User-defined wake phrases are a desirable later feature, but they are not an MVP priority and must not delay making the default phrase excellent.

## Conversation state

Model the core loop explicitly as `Sleeping`, `WakeDetected`, `Listening`, `Transcribing`, `Thinking`, `Speaking`, and `Muted`. Keep only a short in-memory rolling buffer so the beginning of speech is not clipped, use voice-activity detection plus a silence timeout to end an utterance, and share one cancellation path across transcription, model generation, speech synthesis, and playback. For the MVP, pause wake detection while the assistant speaks; add true interruption or barge-in only after echo and self-trigger behavior are tested.

## Provider-neutral design

Keep the voice pipeline composable: local wake word, speech-to-text, text agent, and text-to-speech. Define small asynchronous interfaces for transcription, agent, and speech providers, with capability metadata for streaming, local versus cloud execution, supported formats, voices, cancellation, and optional tools. Provider-specific response objects must not escape into the application core. A provider manifest should hold its display name, endpoint, selected model, nonsecret settings, and a reference to its protected credential.

Hey Computer owns the durable agent substrate: personality, local memory, retrieved context, active tasks, permissions, usage policy, computer-control contracts, and audit history. Treat every cloud or local model as a replaceable reasoning provider. Switching providers must preserve continuity and expose the same provider-independent desktop observations, actions, results, and user-granted capabilities whenever that model can consume the common tool contract. Never make a provider-owned conversation, tool schema, or application the canonical copy of user context or capability state.

## Grok preset

Offer a humorous, human-feeling Grok preset as the initial polished experience, but do not make the application depend on Grok. The preset can use xAI's current Responses and voice capabilities while the generic adapters support other services. Treat legacy OpenAI-compatible chat endpoints as compatibility paths rather than the central architecture. Verify current xAI documentation, model names, pricing, and licensing again when implementation begins because provider surfaces change.

## Local and alternative providers

Support modular adapters so users can combine providers. Ollama is the preferred first local text-model integration because it runs on Windows and exposes a localhost API without forcing the assistant installer to bundle several gigabytes of model data. `whisper.cpp` is the leading offline transcription candidate and should run as a warm resident model rather than reload for every request. Offline speech synthesis remains an open decision: evaluate voice quality, package size, and model licensing before bundling any engine or voice.

## Streaming and perceived speed

Optimize time to the first audible response rather than only total completion time. Play a short local acknowledgement immediately after accepting the wake word, stream audio to transcription where supported, send the committed utterance to the model without unnecessary buffering, collect model text into stable sentence or clause fragments, and synthesize speech ahead of playback. Every stage must be cancellable. Provider-native realtime speech can later improve latency, but the portable modular pipeline remains the canonical baseline.

## Personality configuration

Store personality independently from providers in a versioned profile using readable Markdown plus structured JSON or YAML settings. Compose instructions in ordered layers: non-editable application safety and privacy behavior, the selected personality preset, user customizations, and temporary session context. Describe traits concretely, including brevity, humor, profanity tolerance, initiative, uncertainty handling, and interruption style. Include a voice rule that defaults to one or two spoken sentences unless the user asks for detail.

## Subtle style adaptation

The shipped assistant should preserve a stable core personality while adapting gently to the person speaking with it. Learn low-intensity signals such as preferred reply length, pacing, vocabulary level, humor, directness, filler tolerance, and conversational rhythm, then apply them gradually so the result feels naturally compatible rather than imitative. Do not copy distinctive phrases repeatedly, parody an accent or disfluency, pretend to be the user, or intensify hostility, distress, delusion, or unsafe behavior. Make adaptation optional, locally inspectable, resettable, and subordinate to explicit personality settings and safety rules.

## Memory boundaries

Do not confuse personality with memory. Personality is configuration; memory is user data with provenance, timestamps, confidence, visibility, correction, export, and deletion controls. Post-wake transcripts may be saved automatically long enough for the context agent to summarize them, after which raw transcripts should be discarded according to a clear retention policy; raw audio is not saved by default. Durable memory should use human-readable Markdown plus structured metadata in an Obsidian-compatible folder tree. A SQLite full-text or vector index may accelerate retrieval, but it must be derived and rebuildable rather than the only copy. Provider-owned conversation state is never canonical.

The experience of one continuous consciousness must come from structured retrieval, not an endlessly growing prompt or stored private reasoning. Maintain separate layers for recent conversation, durable facts, decisions, people, projects, active tasks, dated events, and source records. Retrieve only relevant context for each turn and show users why a memory was selected when practical.

## Privacy model

Process the wake phrase locally and keep only the minimum rolling buffer in volatile memory, ideally one or two seconds. Continuously discard pre-wake audio and never write it to disk. After activation, record only until silence, cancellation, or a short hard limit, then transmit only the captured request when the selected provider needs it. Default to no saved audio, no transcript history, no conversational telemetry, and no use for training or analytics by the application.

## Visible microphone state

Make `Muted`, `Waiting locally`, `Recording`, `Processing`, and `Speaking` visually and audibly distinct through the tray icon, a compact overlay, and optional start and stop sounds. Provide push-to-talk, a global cancel shortcut, a prominent software mute, and a menu action that fully stops microphone capture. Onboarding must say plainly that waiting for the wake word still means local microphone processing even though ordinary room audio is not being uploaded.

## Credentials and cost control

Never ship a shared production API key in the desktop client. For bring-your-own-key use, store secrets with Windows Credential Manager and keep only credential references in ordinary settings. Redact secrets and conversation content from logs and crash reports, provide one-click credential removal, disclose which service receives audio, transcripts, prompts, responses, screenshots, and retrieved memories, and surface likely costs. The orchestrator should select the model best suited to each task instead of using the most expensive model universally. Track per-provider usage where APIs permit, estimate it otherwise, cap agent concurrency, and ask before continuing whenever a configured budget is low or a proposed fan-out would exceed its threshold. Future local inference may handle light classification, summarization, retrieval, or routing on capable PCs, but it is not required for the first architecture.

## Onboarding

Keep first run linear: explain privacy, request microphone access, test the microphone and speaker, choose an AI provider, save its credential securely, choose transcription and speech options, run a sample exchange, select a personality, and only then enable the wake phrase with explicit consent. Always offer push-to-talk as a fallback. Include a privacy-oriented path using local components where practical, while being honest about downloads, performance, and quality.

## Installation and updates

Publish a self-contained signed Windows build that needs no development tools. Early testing can use a signed conventional installer; a broad release should use signed MSIX or another mature per-user installer with clean uninstall, explicit login-startup registration, signature-verified updates, and rollback. Do not require administrator access without a concrete need. Use internal, beta, and stable update channels, and consider Store or WinGet distribution only after update and uninstall behavior are reliable.

## Logging and diagnostics

Default local logs to sanitized operational metadata such as application version, state transitions, timing, and error categories. Do not log audio, transcripts, prompts, responses, credentials, contact details, or full provider payloads. Detailed diagnostics should be time-limited and explicitly enabled. Crash reporting and analytics should be opt-in, documented in plain language, stripped of conversational content, and paired with controls to inspect and delete local data.

## Trust and abuse resistance

Design against covert recording, accidental television activation, replayed wake phrases, environmental prompt injection, stolen credentials, unexpected usage charges, harmful model output, and any future attempt to turn untrusted model text directly into computer actions. If actions are added later, give them narrow permissions, preview consequential operations, require confirmation for deletion, purchases, messages, credential access, and security changes, and keep a local action history that the user can inspect.

## MVP contents

The next internal milestone is not unrestricted Jarvis control. It is a vertical slice proving the product architecture: reliable wake and voice conversation; one persistent conversation agent; an asynchronous orchestration agent that can delegate one bounded, harmless task; a context agent that summarizes a completed exchange into an inspectable local vault; a visible task and usage ledger; protected credentials; cancellation; and explicit data-flow indicators. Success means conversation stays responsive while a worker runs, the result returns coherently, memory survives restart and remains editable, and no agent can exceed its permissions or budget silently.

## Deferred features

Defer multiple user or speaker memory profiles, custom wake phrases, fully offline speech, broad unattended automation, purchases, messaging, smart-home integrations, and unrestricted plugin permissions. Local inference for light work is a future differentiator for capable PCs, not a near-term requirement. Deep computer control is a core product direction, but it must arrive incrementally behind a capability broker: direct APIs and MCP first, browser automation second, Windows UI Automation/COM/Win32 next, and screenshot-driven vision as a fallback. Treat each new action class as its own release gate with permissions, audit history, failure recovery, and rollback.

## Testing priorities

Test false accepts and false rejects in quiet rooms and around televisions, music, fans, kitchens, varied accents, different speaking abilities, near and far microphones, laptop arrays, USB headsets, and Bluetooth devices. Exercise sleep and resume, locked screens, lost devices, offline mode, slow networks, provider limits, expired credentials, quota exhaustion, malformed responses, interrupted updates, rollback, and uninstall. Track idle CPU, memory, battery impact, network usage, estimated request cost, wake latency, end-of-speech accuracy, and time to first audible response.

## Initial implementation sequence

Preserve and validate the existing voice foundation, but establish the new supervisory architecture before adding broad integrations. First define the conversation/orchestration/context contracts, local vault schema, task ledger, usage ledger, and capability/confirmation model. Then complete one vertical slice: voice request → conversational acknowledgement → delegated safe worker task → parallel context update → verified result → concise spoken return. After that, add computer control through existing libraries and narrowly scoped adapters, beginning with read-only observation and reversible actions. Resolve the wake-model license, live xAI test, mic calibration, startup registration, signing, and installer validation alongside these milestones rather than allowing them to obscure the product architecture.

## Current status

The project is now a standalone Git repository at `C:\Users\suder\Projects\HeyComputer`. The integrated WPF prototype connects microphone capture, 16 kHz normalization, bounded buffering, push-to-talk, optional local wake detection, the conversation coordinator, xAI transcription and Grok responses, streaming speech playback, the runtime personality preset, Windows Credential Manager, calibration, and reusable onboarding. The credential-free release preflight builds with zero warnings or errors, passes 89 automated tests, creates and verifies the unsigned Windows x64 archive, and confirms that no credentials or model weights are packaged.

The remaining external validation requires user or third-party input: enter an xAI API key through the UI, select a legally redistributable English keyword-spotting model, run real microphone calibration, complete one live xAI conversation, sign the public binaries, and test installer upgrades, uninstall, and update rollback. The English sherpa GigaSpeech KWS model is a quarantined technical candidate and must not be bundled or automatically downloaded until its upstream redistribution and training-data rights are clarified.

## Open questions

Resolve these questions before powerful worker actions or a public release: the worker permission matrix and confirmation boundaries; transcript retention duration and review workflow; the first safe computer-control capabilities; final open-source license and contribution model; whether Grok remains the default after live cost and quality tests; which English wake model has an unambiguous redistribution chain; bring-your-own-key versus managed usage; which light local-inference jobs justify their footprint; and the measured reliability, latency, resource, and budget thresholds that define release readiness. Multiple user or speaker memory profiles are explicitly deferred and should remain on the long-term punch list.

## Decision log practice

When new facts are learned, update the relevant section instead of appending contradictory notes. Record a short dated decision entry only for choices that materially affect architecture, privacy, licensing, cost, or public behavior. Clearly label assumptions and items that still require verification, especially provider APIs, pricing, model licenses, and installer policies that may change over time.
