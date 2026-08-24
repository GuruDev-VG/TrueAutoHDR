# TrueAuto HDR 1.2.4

Developed by **VG Prod.** with extensive assistance from **OpenAI ChatGPT**.

## GitHub update endpoints

TrueAuto HDR now ships with the public GitHub update endpoints configured by default:

- Stable: `https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/stable.json`
- Canary: `https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/canary.json`
- HDR database: `https://raw.githubusercontent.com/GuruDev-VG/TrueAutoHDR/main/update/database.json`

Existing settings files that contain blank update URLs are automatically migrated to these defaults.

The updater also now treats a current-version manifest with no package URL/hash as a normal "nothing newer published yet" state instead of reporting a broken manifest.

Likewise, the maintained HDR database manifest may exist without a downloadable database package yet; PCGamingWiki checking continues normally.

### Important

1.2.4 still needs to be installed/downloaded normally because 1.2.3 did not yet know the GitHub manifest URLs.

Once users are on 1.2.4, later Stable/Canary builds can be distributed through the in-app update system.
