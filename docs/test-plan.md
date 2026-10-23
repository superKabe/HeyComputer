# Test Plan

## Release goals

A public MVP is acceptable only when a nontechnical tester can install it, understand the microphone state, complete a voice turn, cancel it, change providers, mute it, remove credentials, and uninstall it without developer documentation. Tests should measure reliability and privacy behavior, not only whether the happy path works.

## Automated tests

- State-machine transition validity and recovery after cancellation.
- Deterministic fake-provider conversation flow.
- Cancellation propagation through transcription, agent, synthesis, and playback queues.
- Timeout, rate-limit, expired-key, malformed-response, and offline behavior.
- Configuration validation, migration, and redaction.
- No secrets or conversation content in default logs.
- History-disabled persistence checks.
- Installer script parameter validation and repeatable package contents.

## Audio matrix

Test quiet rooms, television, music, fans, kitchens, traffic, near and far microphones, varied accents, different speech abilities, laptop arrays, USB headsets, webcams, Bluetooth devices, and microphone changes during a session. Measure false accepts and false rejects separately. Include assistant playback to prove the MVP cannot wake itself while speaking.

## Windows lifecycle

Exercise sign in, optional startup, multiple launch attempts, lock and unlock, sleep and resume, display changes, default-device changes, microphone removal, network changes, clean shutdown, update interruption, rollback, and uninstall. The tray icon and actual capture state must remain consistent throughout.

## Privacy checks

Verify idle audio never reaches disk or network, the rolling buffer remains bounded, cloud audio begins only after activation, recording obeys silence and hard timeouts, and disabling the microphone closes capture. Inspect logs, settings, crash output, package contents, temporary folders, and network traces for conversation content and credentials.

## Performance measures

Track idle CPU, working-set memory, battery effect, wake-detection latency, end-of-speech latency, first-token latency, first-audio latency, total turn time, network bytes, and estimated request cost. Record hardware, microphone, model, provider region, and network conditions with each result so comparisons are meaningful.

## Human validation

Human testing is required for wake-phrase quality, activation feedback, tone, speech clarity, interruption expectations, and whether privacy explanations are understandable. Do not infer these from automated tests. Use explicit consent and never retain test audio beyond the agreed test purpose.

## Release gates

- Idle resource use meets a documented target on representative low- and mid-range hardware.
- Wake error rates are measured and acceptable in the audio matrix.
- Every microphone and transmission state is obvious.
- Default logs contain no sensitive content.
- Credentials remain protected through update and uninstall choices.
- Network and provider failures recover without runaway retries or billing.
- Startup can be disabled reliably.
- Packages and updates are signed, verified, staged, and reversible.
- Uninstall removes the program and clearly offers to remove retained settings.
- The wake model's weights and companion files have an explicit, preserved redistribution license; no model is inferred to inherit the framework license.

## Credential-free preflight

Run `installer/verify-release.ps1` before any live test. It checks solution coverage, compiles all projects, runs automated tests, builds the portable ZIP, validates its checksums, and rejects packaged model weights, keys, and credential files. Live provider calls, microphone capture, wake accuracy, calibration thresholds, code signing, installer lifecycle, and update rollback remain separate manual release gates.
