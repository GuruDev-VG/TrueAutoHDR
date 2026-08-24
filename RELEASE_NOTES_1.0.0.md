# TrueAuto HDR 1.0.0

Developed by **VG Prod.**

First stable release.

## Highlights

- Automatically enables Windows HDR for games known to support native HDR.
- Restores the previous HDR state after the last HDR game exits.
- Changes HDR on the Windows Main Display only.
- Steam, Epic Games Store, GOG, Xbox/Game Pass, Ubisoft Connect, Rockstar Games Launcher, EA app and standalone EXE discovery.
- Conservative cross-store identity matching with Verified / High / Medium / Low confidence.
- Medium and Low confidence matches never enable HDR automatically.
- Local native-HDR database with independent database-update support.
- Community HDR source review workflow.
- First-run guided setup with curated installed-game scan.
- First-run scan visibly lists verified HDR games and review candidates.
- Lightweight tray/headless startup mode.
- Light, Dark and System appearance options.
- Portable and installer build targets.
- Single-instance protection and persistent crash logging.

## Distribution

Run `BuildPortable.bat` for the portable package.

Install Inno Setup 6 and run `BuildInstaller.bat` for the Windows installer and uninstaller.

`BuildRelease.bat` builds both release targets.

## Validation

The complete 1.0 regression checklist was passed on the target Windows machine before this stable release was finalized.
