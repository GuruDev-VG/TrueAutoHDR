# Changelog

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
