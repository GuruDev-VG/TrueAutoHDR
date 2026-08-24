# TrueAuto HDR 1.2.6

Developed by **VG Prod.** with extensive assistance from **OpenAI ChatGPT**.

## GitHub updater hardening + Game Manager UI startup fix

This is the definitive 1.2.6 source release.

### GitHub updater improvements

- Replaced opaque update downloads with explicitly logged HTTP requests.
- Enabled automatic redirect handling and longer download timeout.
- Added GitHub-friendly request headers.
- Logs requested URL, final URL, HTTP status, content type, and content length.
- Added GitHub Releases API fallback for release assets if the normal download URL fails.
- SHA-256 verification and staged file replacement remain unchanged.

### Game Manager white-flash fix

- Game Manager now starts at 0% opacity.
- WinForms can create native child controls, complete DPI scaling, layout, and theming while invisible.
- The completed themed frame is revealed on the next UI message cycle.
- Added optimized double buffering.
- Finalizes DWM title-bar theming before reveal.
- Adds the diagnostic log entry:
  `Game Manager first-frame reveal completed.`

### Unchanged

No intentional changes to HDR detection behavior, PCGamingWiki verification, storefront detection, watcher performance, or database logic.


### Update staging hotfix

- Fixed the update ZIP remaining open after SHA-256 verification.
- The hash stream is now explicitly disposed before extraction/staging.
- Prevents Windows error: `The process cannot access the file 'update.zip' because it is being used by another process.`
