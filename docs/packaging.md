# Packaging and Updates

## Development package

The repository includes `installer/build-portable.ps1` for an unsigned, self-contained Windows package intended only for local development and trusted testers. It publishes the desktop project, writes a checksum manifest, and creates a ZIP. It does not invent a certificate, bypass signing requirements, or claim the archive is safe for public distribution.

`installer/verify-release.ps1` is the noninteractive release preflight. It verifies solution coverage, restores, builds, tests, packages, validates every packaged checksum, and rejects model binaries and signing or credential material. It needs no API key or microphone. Passing it confirms artifact integrity only; it does not replace live audio, provider, installer, signing, update, or privacy tests.

## Public installer

Use a signed per-user MSIX package or another mature signed installer after application behavior is stable. The installer should avoid administrator access, register sign-in startup only after explicit consent, preserve protected credentials through ordinary upgrades, and offer a clear choice about retained settings during uninstall. Establish publisher identity and certificate management outside the repository.

## Update safety

Public updates must be signed, signature-verified before installation, staged through internal, beta, and stable channels, and reversible. The update client must reject unsigned or mismatched artifacts and must not treat transport encryption alone as artifact authenticity. Record build inputs, dependency versions, checksums, and release approvals.

## CI boundary

Continuous integration may restore, build, test, and produce an unsigned portable artifact. It must not publish that artifact as a public installer or store a long-lived signing certificate without an approved secret-management design. A release job should remain disabled until signing, provenance, rollback, and incident-response procedures exist.

## Release checklist

1. Build and automated tests pass from a clean checkout.
2. Audio, privacy, and Windows lifecycle matrices pass on representative hardware.
3. Provider pricing, endpoints, retention links, and licenses are re-verified.
4. Default package contents and logs contain no secrets or conversation data.
5. The selected wake model has an explicit, model-specific redistribution record; framework licensing alone is insufficient.
6. Binary and installer are signed by the intended publisher.
7. Update signatures and rollback are tested.
8. Install, upgrade, downgrade policy, and uninstall are tested as a standard user.
9. Release notes identify privacy, provider, and data-model changes.
10. Rollout begins in the internal channel and expands only after monitoring.

The live status of these gates is tracked in [release-qa-status.md](release-qa-status.md).

## Store distribution

Consider Microsoft Store or WinGet distribution only after install, update, rollback, and uninstall are dependable outside those channels. Store presence should not substitute for application-level privacy disclosures or provider credential controls.
