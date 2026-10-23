# Providers and Personality

## Implementation status

The Core project currently defines provider interfaces, capability metadata, a provider manifest, personality composition, and deterministic fake providers. No live Grok, Ollama, transcription, or text-to-speech adapter is connected to the desktop yet. This document combines current contracts with target configuration guidance.

## Composable providers

Treat wake detection, transcription, language generation, and speech synthesis as independent selections. Each adapter reports capabilities such as streaming, local or cloud execution, accepted audio formats, voices, tool support, and cancellation. The application core works only with shared contracts; provider-specific request and response types remain inside adapters.

## Initial presets

The Grok preset is the intended polished cloud path, but it is not a dependency. Verify current xAI endpoints, model names, prices, retention terms, and client-token guidance immediately before implementation or release. The first local text option should detect an existing Ollama installation and available models rather than bundling a large model runtime. Offline transcription can later use a resident `whisper.cpp` model. Offline speech remains undecided pending voice-quality and license review.

## Provider manifest

The implemented nonsecret `ProviderManifest` contains a stable provider identifier, display name, service endpoint, selected model, a credential reference, and advanced settings. Capability metadata currently lives on each provider instance rather than in the manifest, and no manifest timeout exists yet. A manifest must never contain an API secret. Advanced endpoints should be clearly labeled because an untrusted endpoint can receive the user's conversation data.

## Credentials

Bring-your-own-key credentials belong in Windows Credential Manager or an equivalent user-scoped protected store. Logs, settings exports, screenshots, crash reports, and support bundles must redact them. The UI must offer a one-click credential removal action. A future managed service should keep master keys on a server, issue short-lived client credentials, enforce quotas, and show costs.

## Personality format

Keep personality independent from the selected model. A profile combines readable Markdown instructions with structured settings for name, response length, humor, profanity tolerance, initiative, uncertainty handling, interruption style, and preferred voice. Version the profile so migrations are explicit.

Compose the effective behavior in this order:

1. Non-editable application safety and privacy behavior.
2. Selected personality preset.
3. User customizations.
4. Temporary session context.

The initial spoken-response rule should request one or two sentences unless the user asks for detail. Provider defaults are never a substitute for the chosen profile.

## Example profile

```yaml
version: 1
name: Dry Helper
style:
  brevity: high
  humor: dry
  profanity: mirror-user
  initiative: moderate
  uncertainty: state-clearly
speech:
  default_sentences: 2
  interruption_style: stop-immediately
```

This example is configuration guidance, not a finalized schema. The implementation should validate unknown fields, preserve forward compatibility where practical, and never allow a personality file to override application privacy controls.
