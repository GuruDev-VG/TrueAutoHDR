# Testing the 1.2.4 -> 1.2.5 in-app update

## Build the update package

Do **not** double-click the `.ps1` file directly.

Run:

```text
BuildStableTestUpdate.bat
```

The launcher keeps the window open if anything fails and writes:

```text
MakeStableTestUpdate.log
```

On success it creates:

```text
UpdatePackages\
  TrueAutoHDR-update-1.2.5.zip
  stable.json
```

## Publish

1. Create GitHub release tag `v1.2.5`.
2. Upload `TrueAutoHDR-update-1.2.5.zip` to that release.
3. Confirm the release asset is downloadable.
4. **Only then** replace repository `update/stable.json` with the generated `UpdatePackages/stable.json`.
5. On TrueAuto HDR 1.2.4, use **Check for app update**.
6. Accept the 1.2.5 Hotfix.
7. The app should close, patch, and restart.
8. Confirm the log contains:
   `Stable update pipeline test build 1.2.5.`

If the package builder fails, send `MakeStableTestUpdate.log`.
