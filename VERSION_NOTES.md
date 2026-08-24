# Version baseline

This repository tree is the definitive **TrueAuto HDR 1.3.0** source.

Key 1.3.0 additions:
- per-game HDR enable delay / exit grace / keep-HDR rules
- per-game display override
- independent per-display HDR session state
- on-demand diagnostics
- application update backups and manual rollback
- production-friendly `BuildUpdatePackage.bat`

All new runtime behavior is event-driven or on-demand; no additional idle polling loops were introduced.
