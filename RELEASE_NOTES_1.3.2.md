# TrueAuto HDR 1.3.2

## Window close behavior

This release improves the behavior of the Game Manager window's close button.

### Changes

- Clicking the Game Manager **X** now asks whether TrueAuto HDR should:
  - keep running in the background/tray; or
  - shut down completely.
- Added **Remember my choice** so the prompt can be skipped for users with a preferred behavior.
- Added a **Closing X** option in Settings:
  - Ask every time
  - Keep running in background
  - Exit application
- Canceling the dialog leaves the Game Manager open.
- Programmatic shutdown, application updates, Windows logoff/shutdown, and tray-menu Exit are not interrupted by the prompt.

### Performance

The close-choice logic runs only when the Game Manager is closed. It adds no polling, background worker, timer, or measurable idle CPU/memory overhead.
