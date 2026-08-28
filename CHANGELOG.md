# Changelog

## 1.5.0 — HDR10+ Gaming, display recovery, and Game Manager redesign

- Added HDR10+ Gaming capability metadata and conservative title-variant inheritance.
- Added per-game display recovery modes for stale HDR/color-pipeline recovery after game exit.
- Redesigned Game Manager around a compact discovery toolbar and selected-game card.
- Added on-demand Steam artwork with local-cache-first behavior.
- Added isolated Stable and Canary update package builders.
- Preserved low-idle-overhead design: no new polling loops or continuous network activity.

## 1.3.2 — Window close behavior

- Added close-window choice between background operation and full application exit.
- Added optional remembered close behavior and Settings override.
- No new background polling or runtime overhead.


## 1.3.1 — Release trust and verification

- Added automatic SHA-256 release hash generation.
- Added SHA-256 sidecar generation for in-app update packages.
- Added optional Authenticode signing hooks without storing signing credentials in the repository.
- Added antivirus/false-positive documentation and reporting guidance.
- Expanded release checklist with artifact verification and AV checks.
- No new runtime polling or background resource usage.


## 1.3.0 — Automation and reliability

- Added per-game HDR timing/exit/display rules.
- Added per-display HDR session state tracking.
- Added on-demand diagnostics.
- Added update backup and manual rollback.
- Kept all new features event-driven/on-demand to protect idle CPU and memory usage.

## 1.2.3 — Public repository preparation

- Prepared the source tree for public GitHub development.
- Added public README, contribution guidance, security guidance, and `.gitignore`.
- Added explicit AI-assisted-development disclosure.
- Retained Stable/Canary update infrastructure and independent HDR-data updating.

## 1.2.x

- Added Stable and Canary application update channels.
- Added separate update-package/updater architecture with SHA-256 verification.
- Unified PCGamingWiki and maintained HDR-database update workflow.
- Added release-build self-testing and updater packaging fixes.

## 1.1.x

- Added PCGamingWiki HDR-list verification.
- Improved DPI/scaling behavior and startup painting.
- Expanded HDR game verification and source/confidence handling.

## 1.0

- First complete portable/installable milestone.
