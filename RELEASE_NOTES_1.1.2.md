# TrueAuto HDR 1.1.2

Developed by **VG Prod.**

## WinForms startup-paint fix

This patch targets the remaining partial white flash when opening Game Manager.

The theme system previously accessed `Form.Handle` while applying the theme. In WinForms that can force the native HWND to be created before the constructor has finished building the control tree, allowing Windows to paint a partially constructed default-white form.

1.1.2 changes this behavior so:
- normal theme application never forces HWND creation;
- controls receive their themed colors while the form is still invisible;
- DWM/title-bar theming is applied only after WinForms naturally creates the finished window handle;
- Per-Monitor V2 DPI handling from 1.1.1 remains unchanged.

HDR detection, PCGamingWiki verification and watcher performance are unchanged.
