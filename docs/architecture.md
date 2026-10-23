# Architecture

## Implementation status

This document describes the target MVP architecture. Today, the WPF shell performs microphone capture and tray lifecycle, while `HeyComputer.Core` implements and tests the provider contracts and coordinator separately. The desktop does not yet run the core conversation pipeline. Wake detection, audio normalization and buffering, live provider adapters, playback, settings persistence, and startup registration remain planned.

## System shape

Hey Computer runs as one per-user Windows tray application with isolated logical runtimes for conversation, orchestration, context maintenance, and task-scoped workers. The Conversation Agent is the only user-facing personality. It exchanges structured events with an Orchestration Agent and a Context Agent so it remains responsive while work and memory maintenance proceed asynchronously. A Windows service is intentionally avoided because microphone routing, notifications, sign-in startup, and user consent belong in the interactive desktop session.

```text
User voice
    │
Conversation Agent ───────────────> spoken response
    │          │
    │          └──> Context Agent ──> local transparent vault
    │
    └──> Orchestration Agent ──> task/usage ledger
                    │
                    ├──> coding worker
                    ├──> browser worker
                    ├──> Windows-control worker
                    └──> other bounded workers
```

## Core flow

```text
Microphone
   │
   ├─ local rolling buffer ─ local wake detector
   │                              │
   │                         wake accepted
   │                              │
   └──────── captured utterance ──┴─> speech-to-text
                                           │
                                      text request
                                           │
                                      AI provider
                                           │
                                    streaming response
                                           │
                                     text-to-speech ─> speaker
```

Only the wake detector reads the idle rolling buffer. After a wake event, the coordinator captures one utterance, runs the selected pipeline, and returns to waiting. The MVP pauses wake detection during playback to avoid self-triggering.

## State machine

The canonical states are `Sleeping`, `WakeDetected`, `Listening`, `Transcribing`, `Thinking`, `Speaking`, and `Muted`. State changes must be observable by the UI and logs without exposing conversation content. Invalid transitions should fail safely into `Muted` or local-only waiting instead of silently recording or repeatedly uploading.

## Component boundaries

- `DesktopShell`: tray icon, microphone test UI, notifications, and lifecycle are implemented; settings and startup choice are planned.
- `AudioEngine`: WASAPI capture, selected device, and device notifications are implemented; resampling, rolling buffer, and pipeline integration are planned.
- `IWakeWordDetector`: local phrase detection with replaceable engines.
- `ConversationCoordinator`: state, timeouts, cancellation, and provider sequencing.
- `ITranscriptionProvider`: audio-to-text streaming or batch transcription.
- `IAgentProvider`: text request to streamed model response.
- `ITextToSpeechProvider`: text fragments to playable audio.
- `PersonalityProfile`: provider-independent behavior and voice preferences.
- `ConversationRuntime`: owns the user-facing turn, personality, interruption, short acknowledgements, and result narration; it never blocks on long-running worker execution.
- `OrchestrationRuntime`: converts accepted intent into bounded tasks, selects models and workers, enforces budgets and capabilities, coordinates dependencies, and validates results.
- `ContextRuntime`: summarizes completed turns and verified outcomes into proposed memory changes with provenance, then commits them through the memory policy.
- `MemoryVault`: human-readable canonical Markdown and structured metadata plus a rebuildable search index.
- `TaskLedger`: durable status, dependency, cancellation, evidence, and recovery state for delegated work.
- `UsageLedger`: provider usage, estimates, configured thresholds, and user approvals for additional spend.
- `CapabilityBroker`: the only path from agents to computer actions; applies scopes, confirmations, audit records, and cancellation.
- `CredentialStore`: protected secret storage and deletion.
- `SettingsStore`: planned nonsecret preferences and provider manifests; no persistent settings store exists yet.

## Cancellation and failure

One cancellation token should stop capture, transcription, model generation, synthesis, and queued playback. Network loss, expired credentials, quota limits, malformed provider responses, device removal, sleep, and resume must leave the application in a visible recoverable state. No error path may retry paid requests indefinitely or keep stale microphone handles alive.

## Latency

Optimize time to first audible response. Acknowledge the wake event locally, stream where provider capability permits, begin synthesis after the first stable clause, and synthesize ahead of playback. Keep modular mode canonical for portability; provider-native realtime mode can be added later behind a separate adapter.

## Deferred architecture

Broad unattended automation, voice identity, multiple memory profiles, purchases, messaging, smart-home control, and unrestricted plugins remain outside the first architecture boundary. Persistent transparent memory, bounded delegation, usage accounting, and a capability broker are now foundational rather than deferred. Computer actions should be introduced incrementally only after their permission and audit contracts exist.
