# TrueAuto HDR 1.1.0

Developed by **VG Prod.**

## PCGamingWiki HDR-list verification

Game verification now checks PCGamingWiki's generated HDR support list directly.

TrueAuto HDR treats these PCGamingWiki states as HDR-capable:

- Native support
- Limited native support
- Always on

`Requires manual fix` and `No native support` are not automatically added.

Exact normalized and canonical-title matches can be accepted automatically. Fuzzy/similar title matches are hints only and never enable HDR automatically.

The PCGamingWiki check runs during:
- first-run curated scan;
- Game Manager installed-game scan;
- Verify selected game.

PCGamingWiki is checked before Steam's HDR category, while the existing local database, Steam and community sources remain available.

If PCGamingWiki blocks or temporarily fails the request, TrueAuto HDR logs the failure and continues with the existing verification sources rather than failing the scan.
