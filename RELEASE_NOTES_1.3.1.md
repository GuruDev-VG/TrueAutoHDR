# TrueAuto HDR 1.3.1

## Release trust and verification

This release improves how TrueAuto HDR builds are verified and distributed after reports of generic antivirus detections on unsigned application/updater binaries.

### Changes

- Release builds now automatically generate `SHA256SUMS.txt`.
- In-app update packages now generate a matching `.sha256` sidecar in addition to the SHA-256 stored in `stable.json`.
- Added optional Authenticode signing hooks for the application, updater, and installer.
- Signing credentials are never stored in the repository.
- Added documentation explaining generic/heuristic antivirus detections and how to verify official release artifacts.
- Added a structured false-positive reporting checklist.
- Expanded the release checklist with hash/signature/AV verification steps.

### Performance

These are build/release-process changes only. They add no new runtime polling, background workers, memory usage, or CPU activity to TrueAuto HDR.
