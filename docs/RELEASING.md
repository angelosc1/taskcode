# Releases

## Build

1. Update `VERSION` and add `docs/release-notes/<version>.md`.
2. Exit Taskcode before compiling its development executable.
3. Prepare Inno Setup 7.1.1 from the pinned official installer:

```powershell
powershell -NoProfile -ExecutionPolicy RemoteSigned -File scripts/prepare-inno.ps1
powershell -NoProfile -ExecutionPolicy RemoteSigned -File scripts/build-release.ps1 -RunChecks
```

The compiler bootstrap verifies the pinned SHA-256 and vendor Authenticode signature. An existing Inno 6.6+ or 7 compiler may also be supplied with `-InnoCompiler`.

Outputs are under `out/release/<version>`: a per-user Windows installer, portable ZIP and `SHA256SUMS`. Packaging uses an explicit six-file allowlist; settings, accounts, local logs, test executables, diagnostic reports and research material are never copied into release assets. Release compilation excludes test classes and diagnostic/demo entry points.

The initial application/installer are unsigned. Do not describe them as signed; checksum verification does not replace publisher signing. Executable signing remains future work.

## Publish

Push the reviewed commit, then an annotated tag matching `VERSION`, for example `v0.1.0`. The release workflow builds and tests on Windows, packages the assets and publishes the matching release notes. GitHub supplies the asset SHA-256 digests used by the updater.

After publishing, verify asset names and digests against `SHA256SUMS`, install the setup edition, connect accounts, confirm saved preferences, and test **General > Check for updates**. A local older test build can exercise upgrade to the public version without publishing a dummy release.

Do not replace binaries in an existing release with different bytes. Publish a new version instead.
