# TrueAuto HDR update manifests

These files provide fixed URLs for TrueAuto HDR's independent update channels.

- `stable.json` — normal releases and production hotfixes.
- `canary.json` — opt-in experimental releases.
- `database.json` — separately maintained native-HDR database.

## Application releases

When publishing a new Stable or Canary build:

1. Build the new TrueAuto HDR version.
2. Generate `TrueAutoHDR-update-VERSION.zip`.
3. Upload the update ZIP as a GitHub Release asset.
4. Calculate/use the ZIP's SHA-256.
5. Update the appropriate manifest with:
   - `version`
   - `releaseType`
   - permanent GitHub Release asset URL in `packageUrl`
   - SHA-256 in `sha256`
   - short release notes
6. Commit the manifest change only after the release asset is online.

Do not point Stable at a Canary package.

The intended permanent raw manifest URLs are:

Stable:
https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/stable.json

Canary:
https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/canary.json

Database:
https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/database.json

The empty package/hash fields in the initial manifests are intentional. Version 1.2.3 predates the first update package and should not attempt to update itself to itself.
