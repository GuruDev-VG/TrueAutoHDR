# TrueAuto HDR 1.2.1

Developed by **VG Prod.**

## Release pipeline hotfix

1.2.0's release builder could produce incomplete output because the separately built updater was never copied into the portable or installer payload.

1.2.1 fixes the release pipeline:

- `Build.bat`, `BuildPortable.bat`, and `BuildInstaller.bat` build and copy `TrueAutoHDR.Updater.exe`.
- The updater is self-contained.
- Every produced payload verifies required files before packaging.
- Every produced payload runs `TrueAutoHDR.exe --self-test`; packaging stops on any non-zero result.
- `BuildRelease.bat` stops immediately if either target fails.
- The update runner is copied to staging before execution, allowing an update package to replace `TrueAutoHDR.Updater.exe` safely.

This version should be used as the new 1.2 update-system baseline.
