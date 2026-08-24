# TrueAuto HDR 1.1.4

Developed by **VG Prod.**

## Unified HDR source update

The existing **Check database update** action now performs two stages in order:

1. **PCGamingWiki HDR list**
   - Forces a fresh download of the PCGamingWiki HDR support list.
   - Checks installed games using the same rules as first-run setup.
   - `Native support`, `Limited native support`, and `Always on` are added as HDR-capable.
   - Fuzzy title matches are logged/ignored and never enable HDR automatically.
   - Explicit user HDR/SDR overrides are preserved.

2. **TrueAuto HDR maintained database**
   - Checks the independently hosted JSON database manifest.
   - Downloads and validates a newer database when available.
   - Continues even if PCGamingWiki is temporarily unavailable.

This combined flow is used from Game Manager, Settings, and the tray menu.
