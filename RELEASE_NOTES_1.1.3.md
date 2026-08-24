# TrueAuto HDR 1.1.3

Developed by **VG Prod.**

## WinForms DPI configuration cleanup

- Removed legacy DPI declarations (`dpiAware`, `dpiAwareness`, `gdiScaling`) from `app.manifest`.
- `ApplicationHighDpiMode=PerMonitorV2` remains configured in the .NET project.
- The early `Application.SetHighDpiMode(PerMonitorV2)` / Win32 DPI bootstrap remains in place for startup robustness.
- The manifest still declares Windows compatibility and long-path awareness.

This removes compiler warning `WFAC010` without reverting the reboot/startup DPI fixes from 1.1.1/1.1.2.
