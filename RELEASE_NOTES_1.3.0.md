# TrueAuto HDR 1.3.0

Developed by **VG Prod.** with extensive assistance from **OpenAI ChatGPT**.

## Automation, diagnostics and update safety

1.3.0 expands TrueAuto HDR's automation while keeping the idle watcher lightweight.

### Per-game rules

Select a game in Game Manager and open **Per-game rules** to configure:

- HDR enable delay (0–30 seconds)
- Exit grace period (0–30 seconds)
- Keep HDR enabled after that game exits
- HDR display override

The default remains the Windows Main Display. A rule can instead target another currently connected display.

Rules are read only when a game starts/exits. They add no new idle polling loop.

### Better launcher/process resilience

The exit-grace rule helps games launched through wrappers that briefly close or replace their game process. If the same game returns during the grace period, the existing HDR session is preserved instead of toggling HDR unnecessarily.

### Multi-display session tracking

HDR state is now remembered independently per selected display. Two HDR games targeting different monitors can coexist without sharing one global saved HDR state.

### On-demand diagnostics

A new **Diagnostics** window is available from the tray menu.

It reports, on demand:

- TrueAuto HDR/.NET/Windows version information
- current process working set
- update channel/startup state
- HDR database/rule counts
- main-display HDR support/state
- connected displays
- installed-game count

No diagnostics telemetry or background diagnostics worker runs while the window is closed.

### Safer application updates / rollback

Before replacing application files, the updater now backs up the files being changed.

Settings exposes **Rollback last update** when a backup exists. Rollback restores application files only; settings, logs, game rules and the user's HDR database remain untouched.

Backup retention is bounded so old update backups do not grow indefinitely.

### Performance philosophy

No additional permanent polling loops were added in 1.3.0. Existing one-second process discovery remains the primary idle watcher. Per-game delays use asynchronous waits only while relevant games are starting/exiting, diagnostics are generated on demand, and update backups exist only during/after an application update.
