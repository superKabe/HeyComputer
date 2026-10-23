# Privacy Model

## Status and scope

This is the required privacy design for the MVP, not a claim about the current prototype. The present desktop shell captures microphone audio locally for device testing and does not connect to wake, transcription, model, or speech providers. It does not implement the planned rolling buffer, activation gate, recording timeout, history controls, privacy dashboard, or provider disclosures yet. Do not use the prototype as an always-listening assistant until those controls are implemented and verified.

## Target default behavior

Wake detection runs locally. The application keeps only the smallest useful pre-roll buffer in memory, targets one or two seconds, continually overwrites it, and never writes it to disk. After activation, recording stops on silence, cancellation, or a short hard timeout. Only the activated utterance is sent to a cloud speech provider, and only when the selected configuration uses one.

## Data defaults

- Saved audio: off.
- Transcript history: off.
- Prompt and response history: off.
- Conversational telemetry: off.
- Crash reporting and usage analytics: opt-in.
- Diagnostics: sanitized operational metadata only.
- Provider credentials: Windows-protected storage, never ordinary settings.

## User-visible states

The interface must distinguish `Muted`, `Waiting locally`, `Recording`, `Processing`, and `Speaking`. Waiting locally still means the microphone is being processed on the computer, so onboarding must say this directly. Recording begins with a visible indicator and optional tone. The user can cancel the active turn, use push-to-talk instead of a wake phrase, or completely stop microphone capture.

## External recipients

The settings interface must show which services receive audio, transcripts, prompts, and generated responses. Changing a provider must update that explanation before the next request. Provider retention and training policies belong to the provider and may change, so link to the current policy and avoid claiming the desktop application can override it.

## Logs and diagnostics

Default logs may include version, state transitions, timing, selected provider type, and sanitized error categories. They must not include raw audio, transcripts, prompts, responses, API keys, authorization headers, contacts, or complete provider payloads. Temporary detailed diagnostics require explicit enablement, an expiry, a clear warning, and a deletion action.

## History and memory

Personality is configuration; memory is user data. If history is later enabled, it must be locally visible, selectively deletable, exportable, and independently configurable from provider-side retention. Permanent memory must never be inferred silently from ordinary speech. A short session transcript and explicit user-pinned facts are the maximum initial scope.

## Shared spaces

The computer owner's consent does not automatically cover guests, coworkers, or family members. Provide a conspicuous mute control and recommend push-to-talk in shared rooms, workplaces, and locations with stricter recording-consent rules. Do not claim speaker identification or consent detection in the MVP.

## Data controls

One privacy screen should expose the active microphone, wake engine, transcription service, model provider, speech service, history status, startup status, and the most recent transmission time and destination without retaining its content. Include `Delete local history`, `Remove credentials`, `Disable microphone`, and `Reset application` actions.
