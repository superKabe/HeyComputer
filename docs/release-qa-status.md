# Release QA Status

## Automated foundation

The credential-free preflight is implemented in `installer/verify-release.ps1`. It requires every project under `src` and `tests` to belong to `HeyComputer.sln`, then restores, builds, tests, packages, verifies package checksums, and rejects model binaries and secret-bearing files. On October 21, 2026, the integrated solution passed with zero build warnings or errors and all 89 automated tests green. The unsigned Windows x64 portable ZIP was rebuilt and its complete checksum manifest passed; it contained no model weights, signing keys, or credential files.

## Wake-model blocker

The wake runtime and deterministic tests exist, but no model is approved for public redistribution. The English GigaSpeech candidate remains quarantined pending an explicit and preserved rights chain covering its weights, vocabulary, BPE asset, commercial use, and redistribution. Public packages must remain model-free until the requirements in `models/licensing-audit.md` are met.

## Human and credential gates

The following cannot be certified by offline CI and remain required before a usable release:

- A real Windows microphone test for capture, normalization, VAD, wake detection, device changes, sleep and resume, and cancellation.
- Multi-speaker positive and long-duration negative wake trials under the documented acoustic protocol.
- A user-supplied xAI key test covering STT, streamed model output, TTS playback, cancellation, rate limits, and sanitized failures.
- Verification that raw idle audio, transcripts, prompts, responses, and secrets never enter default logs or persistent settings.
- First-run onboarding, calibration, credential removal, mute, reset, and uninstall testing by a nontechnical user.
- Idle CPU, memory, battery, latency, network-byte, and estimated-cost measurements on low- and mid-range hardware.

## Distribution gates

The portable ZIP is an unsigned development artifact, not an installer. Public release still requires publisher identity, code signing, signature verification, a per-user installer, opt-in startup registration, upgrade and rollback behavior, standard-user uninstall, retained-settings choices, and staged update testing. CI must not present an unsigned ZIP as a public release.

## Exit criteria

Release readiness requires one clean run of the credential-free preflight from a clean checkout, followed by documented passes for the wake, privacy, provider, lifecycle, performance, signing, install, update, rollback, and uninstall gates. A green unit-test count alone is not sufficient.
