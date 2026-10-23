# Hey Computer

Hey Computer is a prototype for a lightweight Windows voice assistant. The target product will process its wake phrase locally, show microphone state clearly, and keep speech and AI providers replaceable. The initial release is intended to be a conversation client, not a general computer-control agent.

## Current status

The repository contains an integrated Windows prototype. The WPF tray app captures and normalizes microphone audio, supports push-to-talk and conditional local wake detection, and connects the tested conversation coordinator to xAI transcription, Grok responses, streaming speech playback, the runtime personality preset, and Windows Credential Manager. Missing credentials or wake models produce explicit setup states rather than misleading privacy claims.

The credential-free release preflight currently builds with zero warnings or errors, passes 89 automated tests, creates the Windows x64 portable archive, verifies its checksum manifest, and confirms that no credentials, signing keys, or model weights were packaged. Real microphone behavior, wake accuracy, one live xAI conversation, code signing, installer lifecycle, and update rollback still require external validation.

## Target experience

The integrated executable implements this flow when valid credentials and a compatible local wake model are configured; push-to-talk remains available without a wake model:

1. Hey Computer waits locally for the phrase “Hey Computer,” or the user presses a push-to-talk shortcut.
2. A visible indicator and optional tone show that recording has started.
3. The captured request is transcribed and sent to the user-selected AI provider.
4. The reply is synthesized and played while the application remains cancellable.
5. The application returns to local wake-word mode and does not retain the conversation unless the user enables history.

## Design principles

- Local wake-word processing with no continuous cloud audio stream.
- Clear `Muted`, `Waiting locally`, `Recording`, `Processing`, and `Speaking` states.
- Independent wake-word, transcription, AI, and speech providers.
- Bring-your-own-key credentials stored with Windows protection.
- No saved audio, transcript history, or conversational telemetry by default.
- One per-user tray process instead of a Windows service.
- Computer control, persistent memory, and unattended actions deferred until separately secured.

## Selected and planned stack

- C# targeting .NET 10 for the current projects.
- WPF for the Windows tray and settings interface.
- NAudio with WASAPI shared-mode microphone capture.
- A replaceable local wake-word detector, with sherpa-onnx as the leading open candidate.
- Provider interfaces for speech-to-text, text generation, and text-to-speech.
- Grok as an optional polished preset, not an architectural dependency.
- Ollama as the first local text-model integration.

## Repository map

- `src/HeyComputer.Core`: conversation state, provider contracts, configuration, and orchestration.
- `src/HeyComputer.Desktop`: integrated Windows tray application and runtime composition.
- `src/HeyComputer.Audio`: normalization, buffering, pre-roll, VAD, and utterance segmentation.
- `src/HeyComputer.WakeWord`: local sherpa-onnx detector boundary and model validation.
- `src/HeyComputer.Providers.Xai`: xAI transcription, Responses, and speech adapters.
- `src/HeyComputer.Settings`: runtime personality and nonsecret settings persistence.
- `src/HeyComputer.Calibration`: first-run wake-word measurement and completion records.
- `src/HeyComputer.Onboarding`: reusable WPF onboarding and settings flow.
- `config`: the versioned default runtime personality.
- `models`: model provenance policy and quarantined candidate metadata; no weights are committed.
- `tests`: deterministic automated tests.
- `docs`: architecture, privacy, providers, onboarding, threats, and test strategy.
- `installer`: repeatable local publishing and packaging guidance.

## Developer quick start

Install the .NET 10 SDK on Windows, then run these commands from the `HeyComputer` directory:

```powershell
dotnet restore .\HeyComputer.sln
dotnet build .\HeyComputer.sln --configuration Release --no-restore
dotnet test .\HeyComputer.sln --configuration Release --no-build
```

To create an unsigned portable test package:

```powershell
.\installer\build-portable.ps1
```

To run the complete credential-free release preflight:

```powershell
.\installer\verify-release.ps1
```

The portable package is for development and trusted testing only. A public release must use code signing, verified updates, privacy review, and the release gates in [the packaging plan](docs/packaging.md).

## Documentation

- [Architecture](docs/architecture.md)
- [Privacy model](docs/privacy.md)
- [Provider and personality configuration](docs/providers-and-personality.md)
- [Threat model](docs/threat-model.md)
- [Test plan](docs/test-plan.md)
- [First-run onboarding copy](docs/onboarding.md)
- [Packaging and updates](docs/packaging.md)

## Contribution boundary

Do not add computer control, automatic shell execution, filesystem access, messaging, purchases, or similar consequential actions to the initial voice client. Each such capability needs a separate permission model, confirmation design, audit trail, abuse review, and rollback plan.
