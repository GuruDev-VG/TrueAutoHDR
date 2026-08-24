# TrueAuto HDR 1.0 — Release / Regression Checklist

This checklist is intended for the final Windows build machine.

## Build

- [ ] `BuildPortable.bat` completes with 0 errors.
- [ ] Portable ZIP contains `TrueAutoHDR.exe`, `portable.mode`, and the `Database` folder.
- [ ] Portable mode creates `Data\` beside the executable and does not create `%LOCALAPPDATA%\TrueAutoHDR`.
- [ ] `BuildInstaller.bat` completes with .NET 8 SDK + Inno Setup 6 installed.
- [ ] Installer output is `release\Installer\TrueAutoHDR-1.0.0-Setup.exe`.
- [ ] Installed app appears in Windows Installed apps / Add or remove programs.
- [ ] Uninstaller launches successfully.

## First run / onboarding

- [ ] Fresh installed build opens the three-step setup wizard.
- [ ] Welcome page explains main-display-only HDR behavior.
- [ ] Setup scan detects expected Steam / Epic / GOG / Xbox / Ubisoft / Rockstar / EA games.
- [ ] Steam-confirmed HDR games are counted/added automatically.
- [ ] Medium/Low cross-store or community matches appear unchecked for review.
- [ ] Finishing setup opens Game Manager.
- [ ] Closing setup without completing it causes setup to appear again next time Game Manager is opened.
- [ ] `--startup` never opens the setup wizard by itself.
- [ ] “Run setup wizard…” in the tray menu opens it again after onboarding is complete.

## HDR behavior

- [ ] Native-HDR game launch enables HDR on the Windows Main Display only.
- [ ] Secondary monitors are untouched.
- [ ] Exiting the last HDR game restores the Main Display’s previous HDR state.
- [ ] If HDR was already ON before game launch, it remains ON after game exit.
- [ ] Running two HDR games keeps HDR ON until both exit.
- [ ] Medium/Low identity candidates never toggle HDR until explicitly approved.

## Launcher detection

- [ ] Steam
- [ ] Epic Games Store
- [ ] GOG
- [ ] Xbox / Game Pass public `XboxGames` layout
- [ ] Ubisoft Connect
- [ ] Rockstar Games Launcher
- [ ] EA app / Origin-compatible metadata
- [ ] Standalone EXE registration

## Database

- [ ] Bundled native HDR database seeds correctly on first run.
- [ ] User overrides remain separate from native database updates.
- [ ] Database updater accepts a valid manifest.
- [ ] Invalid/empty database downloads are rejected.
- [ ] SHA-256 mismatch is rejected when a hash is provided.
- [ ] Database update does not require an app update.
- [ ] Cross-store IDs and aliases in newer DB entries remain backward compatible with old entries.

## Startup / performance

- [ ] “Run at startup” creates a registry command ending in `--startup`.
- [ ] Startup mode remains tray/headless and waits before launcher/process work.
- [ ] Idle CPU returns to ~0% with only brief process-change activity.
- [ ] Idle memory remains in the expected low tens of MB.
- [ ] Launching TrueAuto HDR twice does not create two tray instances.

## UI / DPI

- [ ] Dark / Light / System themes.
- [ ] 100%, 125%, 150%, and 200% display scaling.
- [ ] Move Game Manager between monitors with different DPI scaling.
- [ ] No ghost borders / stretched card rendering after resize.
- [ ] Progress bars stop animating after scans.
- [ ] Tray color-wheel icon is legible at Windows tray size.

## Installer / uninstall

- [ ] Optional desktop shortcut works.
- [ ] Start Menu shortcut works.
- [ ] Installer launches TrueAuto HDR after setup.
- [ ] Uninstall removes TrueAuto HDR startup registry entries.
- [ ] Uninstaller asks whether to remove `%LOCALAPPDATA%\TrueAutoHDR`.
- [ ] Choosing No preserves user data for reinstall.
- [ ] Choosing Yes removes settings, logs, local DB and custom games.
