# Installer Tools

`build-portable.ps1` creates a self-contained Windows ZIP for local development and trusted testing. It deliberately produces an unsigned package and stops if it cannot identify exactly one desktop project.

Run from the `HeyComputer` directory:

```powershell
.\installer\build-portable.ps1
```

Optional parameters:

```powershell
.\installer\build-portable.ps1 -Runtime win-x64 -Configuration Release
```

Do not distribute the result as a public release. Public packaging requires publisher identity, code signing, signature verification during updates, rollback testing, and the release gates in `docs/packaging.md`.

Run the complete credential-free release check with:

```powershell
.\installer\verify-release.ps1
```

This verifies that every source and test project belongs to the solution, restores, builds, runs all automated tests, creates the unsigned portable package, validates its checksum manifest, and rejects bundled model weights, private keys, or credential files. It deliberately does not launch the application, access a microphone, call xAI, sign code, or claim that human wake-word testing passed.
