# TrueAuto HDR 1.2.5

Developed by **VG Prod.** with extensive assistance from **OpenAI ChatGPT**.

## Stable updater pipeline test

This is intentionally a minimal release.

Changes:
- Version bumped from 1.2.4 to 1.2.5.
- Added a startup log marker: `Stable update pipeline test build 1.2.5.`
- No HDR detection, UI, database, DPI, launcher, or performance behavior changed.

Purpose:
- Validate the complete Stable update path:
  GitHub manifest -> update ZIP -> SHA-256 verification -> staged updater -> file replacement -> restart as 1.2.5.

After the update succeeds, TrueAuto HDR should report version 1.2.5 and the log should contain the test marker.
