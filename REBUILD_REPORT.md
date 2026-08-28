# TrueAuto HDR 1.4.0 reconstruction report

Baseline: `TrueAutoHDR-v1.3.2-tidy-close-dialog-source.zip` from the current ChatGPT session.

## Reconstructed / added

- Stable version metadata moved to 1.4.0.
- Game Manager redesigned around a compact Discover & Verify toolbar, selected-game information card, compact game table, and compact database footer.
- Selected-game HDR override is now one dropdown: Automatic / Native HDR / SDR-Disabled.
- Steam artwork service added. It checks local Steam grid art first, then uses the standard Steam CDN only for the selected game, and caches successful art under LocalAppData. No scrolling prefetch or background artwork polling was added.
- HDR10+ Gaming metadata added as a capability distinct from Native HDR decisions and Display Recovery.
- Added 17 HDR10+ Gaming title records/metadata, including aliases for Crimson Desert Enhanced and Call of Duty Modern Warfare 4 Beta.
- Conservative automatic identity inheritance added for known non-identity title suffixes such as Enhanced, Beta, Open Beta, Demo, Trial, and common edition suffixes.
- Capability-only HDR10+ records cannot become Native HDR decisions simply because they exist.
- Existing user Native HDR overrides do not hide bundled HDR10+ capability metadata.
- Per-game Display Recovery added: Off, Re-apply current display mode, Force refresh-rate reset. It runs only when the configured game exits.
- Stable and Canary update builders split into isolated output paths and manifests.
- Canary build uses the `CANARY` compile constant. A RenoDX/Special K HDR Mod Discovery foundation is included behind Canary-only UI wiring; RenoDX refresh is explicit through Refresh HDR Data and cached locally. Stable does not expose mod-discovery controls.
- Approved Game Manager visual reference is stored in `Design/GameManager-1.4-redesign-reference.png`.
- Existing close-dialog behavior, updater rollback/hash verification, optional signing hooks, and PowerShell 5.1-compatible release trust files were preserved from the baseline.

## Performance constraints retained

- Existing one-second process watcher remains unchanged in cadence.
- No new idle polling loop was added.
- No new background timer was added.
- Steam artwork is selection-driven and cached.
- RenoDX Canary refresh occurs only through an explicit data refresh action.
- Display Recovery executes only on the configured game's exit event.

## Validation performed in this environment

- All project JSON files parse successfully.
- Both project XML files parse successfully.
- C# source was lexically checked for balanced braces, parentheses, and brackets.
- The source tree contains only the current `RELEASE_NOTES_1.4.0.md` release-note file; historical release information remains in `CHANGELOG.md`.
- Stable/Canary update scripts were reviewed for path isolation.

## Build limitation

The execution environment used for this reconstruction does not contain the .NET SDK or a Windows WinForms runtime, so a Windows binary could not be compiled or launched here. The canonical source is prepared for a Windows/.NET 8 build using `Build.bat`, `BuildRelease.bat`, `BuildStableUpdate.bat`, or `BuildCanaryUpdate.bat`.

Before publishing a release, run the Release build/self-test on Windows and visually inspect the Game Manager at 100%, 125%, 150%, and 200% display scaling.

## Exact visual pass

The Game Manager received a second visual implementation pass based on the approved 1600x960 reference:
- navy-black modern surface palette and subtle rounded card borders;
- gradient purple primary Scan Library button;
- compact rounded secondary buttons and purple-outline HDR Options action;
- rounded search field with search glyph;
- compact metrics strip with HDR/HDR10+ semantic colors;
- selected-game cover card, store pill, status pills and override control;
- rounded game-list card with compact headers and semantic HDR/HDR10+ indicators;
- compact Database & Advanced footer with database version and ready state;
- no new polling/background network behavior.

The exact approved visual reference is stored at `Design/GameManager-1.4-redesign-reference.png`.
