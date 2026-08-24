# TrueAuto HDR

**Automatically enable Windows HDR when a native-HDR game starts, and turn it back off when the game closes.**

TrueAuto HDR is a lightweight Windows tray utility for people who normally keep HDR disabled on the desktop but want it enabled automatically for games that support HDR natively.

> **TrueAuto HDR is an independent community project and is not affiliated with Microsoft. It is not the Windows Auto HDR feature.**

## What it does

TrueAuto HDR watches for installed/running games, identifies titles with native HDR support, enables HDR on the **main monitor** when an HDR game starts, and restores HDR when the game exits.

Game verification can use:

- a bundled/maintained TrueAuto HDR database;
- the PCGamingWiki HDR support list;
- Steam store metadata;
- community-based title matching;
- manual user overrides.

PCGamingWiki entries tagged **Native support**, **Limited native support**, or **Always on** can be recognized as HDR-capable. Fuzzy title matches are treated conservatively and are not allowed to enable HDR automatically.

## Features

- Automatic HDR switching for native-HDR games
- Windows Main Display by default, with optional per-game display overrides
- Steam and additional storefront/game detection
- PCGamingWiki HDR-list verification
- Local HDR database and manual game management
- Candidate review for uncertain matches
- Portable and installable builds
- Run at Windows startup / minimized startup mode
- Light and dark UI
- Tray operation
- Separate HDR database updates
- Stable and opt-in Canary application update channels
- SHA-256 verification for application update packages
- Lightweight watcher designed to stay out of the way while gaming

## Stable and Canary updates

TrueAuto HDR separates application updates from HDR-data updates.

**Stable** is intended for normal releases and production hotfixes.

**Canary** is opt-in and intended for experimental features and changes that may contain regressions.

HDR support data is updated independently through PCGamingWiki checks and the separately maintained TrueAuto HDR database.

## Building

Requirements:

- Windows 10/11
- .NET 8 SDK
- Inno Setup 6 only if you want to build the installer

For a normal local build:

```bat
Build.bat
```

For the complete release:

```bat
BuildRelease.bat
```

The release pipeline builds the updater and application, optionally Authenticode-signs release executables when signing is configured, verifies required payload files, runs the built-in `--self-test`, packages Portable/Installer builds, and generates `release/SHA256SUMS.txt`.

See [SIGNING.md](SIGNING.md) for the optional signing setup.

## AI-assisted development

**TrueAuto HDR is an AI-assisted software project.**

The project was created and developed by **VG Prod. with extensive assistance from OpenAI's ChatGPT**, including code generation, debugging, refactoring, UI iteration, architecture discussions, and documentation.

AI-generated or AI-assisted code is reviewed and tested as part of development, but—as with any software—bugs are possible. This disclosure is intentional. If you prefer not to use software developed with generative-AI assistance, this project may not be for you.

Constructive bug reports, technical criticism, testing, and contributions are welcome. Arguments whose only purpose is objecting to the use of AI are not useful to the project.

## PCGamingWiki

TrueAuto HDR can consult PCGamingWiki's community-maintained HDR information to help determine whether an installed game supports native HDR.

PCGamingWiki is an independent project and is not affiliated with TrueAuto HDR or VG Prod. Please support and contribute corrections to PCGamingWiki when you find inaccurate game information.


## Antivirus detections and release verification

TrueAuto HDR performs several operations that can attract heuristic antivirus attention: it watches for game processes, changes Windows HDR state, downloads verified updates, and uses a separate updater that replaces application files before restarting the program.

Generic detections such as `IDP.Generic`, `Malware-gen`, `Generic`, or `Heur` do not identify one specific malware family. However, an antivirus warning should still be treated seriously rather than automatically ignored.

For official releases:

- download only from this repository's GitHub Releases page;
- verify the release file against the published `SHA256SUMS.txt`;
- do not disable or whitelist your antivirus solely because a detection is believed to be a false positive;
- report unexpected detections so the exact build can be reviewed and submitted to the antivirus vendor if appropriate.

See [ANTIVIRUS.md](ANTIVIRUS.md) for verification and reporting details.

## Privacy

TrueAuto HDR is designed as a local desktop utility. Game detection and HDR switching happen locally. Features that check PCGamingWiki, storefront metadata, databases, or application updates necessarily make network requests to those sources.

## Contributions

Issues and pull requests are welcome. Please keep reports technical and reproducible where possible.

Useful issue reports include:

- TrueAuto HDR version
- Windows version
- display configuration and scaling
- affected game/storefront
- relevant TrueAuto HDR log lines
- steps needed to reproduce the problem

See [CONTRIBUTING.md](CONTRIBUTING.md) for more.

## Status

TrueAuto HDR is under active development. Back up anything important and expect Canary builds in particular to occasionally break.

## License

TrueAuto HDR is free and open-source software licensed under the **GNU General Public License v3.0 or later (GPL-3.0-or-later)**.

You may use, study, modify, and redistribute the project under the terms of the GPL. If you distribute a modified/derivative version covered by the GPL, the corresponding source code must remain available under compatible GPL terms.

See [LICENSE](LICENSE) for the full license text.

## Credits

Developed by **VG Prod.**

Created with extensive development assistance from **OpenAI ChatGPT**.

Game HDR compatibility information may be sourced from **PCGamingWiki** and other supported metadata/community sources.
