# Threat Model

## Protected assets

The primary assets are private room audio, transcripts, prompts and responses, provider credentials, billing limits, local history, microphone state, and user trust. Future action permissions and action history become protected assets only when computer-control features are deliberately introduced.

## Trust boundaries

Trust boundaries exist between the microphone and local application, local application and each provider, settings and protected credential storage, update channel and installed executable, diagnostics and support recipients, and generated model output and any future action executor. Model output is always untrusted, even when it sounds confident.

## Major threats and controls

- **Covert recording:** visible microphone states, a hard mute, local wake processing, bounded recording, and no default audio storage.
- **False activation:** calibrated wake thresholds, activation tones, fast cancellation, push-to-talk, and testing with television, music, and household noise.
- **Replay activation:** do not promise prevention in the MVP; bound recording and make transmission visible. Consider optional speaker checks only after a separate accuracy and privacy review.
- **Environmental prompt injection:** treat speech and model output as untrusted content; the MVP has no automatic action executor.
- **Credential theft:** Windows-protected storage, redacted logs, no shared embedded key, least-privilege support bundles, and easy credential removal.
- **Unexpected charges:** clear provider ownership, request cancellation, bounded retries, optional session and monthly limits, and visible quota errors.
- **Malicious provider or endpoint:** show the destination and data types before use, validate transport security, and warn on custom endpoints.
- **Supply-chain or update compromise:** signed releases, verified update signatures, staged rollout, rollback, dependency review, and reproducible build records.
- **Sensitive diagnostics:** content-free default logs, opt-in temporary diagnostics, expiry, preview, and deletion.
- **Unsafe future actions:** narrow permissions, previews, confirmation for consequential actions, local audit history, and no privilege inheritance by plugins.

## Failure posture

When microphone, device, credential, provider, or update state is uncertain, fail visibly and reduce capability. Prefer `Muted` or local-only waiting over silent recording, unexpected uploads, automatic credential replacement, or infinite retries. A failure must never leave the UI claiming the microphone is off while capture continues.

## Out of scope for MVP

The first release does not defend a compromised Windows account, hostile administrator, kernel malware, or malicious microphone firmware. It also does not authorize filesystem, browser, messaging, purchase, security-setting, or shell actions. State these limits plainly rather than implying broad protection.

## Review triggers

Repeat threat review before adding persistent memory, speaker identity, custom plugins, provider-native realtime sessions, a hosted billing backend, any computer action, or any telemetry containing conversation-derived data. Also repeat it after a material authentication, installer, update, or credential-storage change.
