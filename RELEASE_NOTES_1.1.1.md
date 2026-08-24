# TrueAuto HDR 1.1.1

Developed by **VG Prod.**

## DPI / scaling stability

- Sets Per-Monitor V2 DPI awareness at the Win32 process level before WinForms initializes.
- Adds an explicit Windows application manifest declaring PerMonitorV2.
- Keeps GDI scaling disabled.
- Recalculates WinForms DPI layout after each major form receives its real HWND/monitor DPI.
- Intended specifically to fix incorrect UI scaling after Windows reboot / automatic startup.

No HDR watcher or performance behavior was changed.
