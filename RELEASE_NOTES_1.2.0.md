# TrueAuto HDR 1.2.0

Developed by **VG Prod.**

## Program update / hotfix system

TrueAuto HDR now has a separate application-update pipeline in addition to HDR-data updates.

### Channels
- **Stable** — normal releases and production hotfixes.
- **Canary** — opt-in experimental builds. The UI warns before enabling Canary.
- **HDR database** — remains independent and continues using PCGamingWiki + the maintained TrueAuto HDR DB.

### Safety
- Update ZIPs are verified with SHA-256 before extraction.
- Updates are staged outside the installation directory.
- A separate `TrueAutoHDR.Updater.exe` waits for the main application to exit before replacing files.
- Package extraction and updater copy paths are constrained to their intended directories.

### Publishing
`MakeUpdatePackage.ps1` creates an update ZIP plus a manifest containing its SHA-256.

Stable and Hotfix releases should publish to the Stable manifest. Canary experiments publish to the Canary manifest.
