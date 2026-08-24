# Antivirus detections and release verification

TrueAuto HDR is a small open-source Windows utility that performs actions which can attract heuristic antivirus attention:

- watches for game processes;
- changes Windows HDR state;
- downloads application updates;
- verifies update packages;
- launches a separate updater;
- the updater replaces application executables and restarts TrueAuto HDR.

These behaviors can resemble patterns used by unwanted software even when the program itself is legitimate.

## Generic detections

Names such as `IDP.Generic`, `Malware-gen`, `Generic`, or `Heur` usually indicate a heuristic/generic classification rather than identification of one specific malware family.

A generic name alone is **not proof that a detection is a false positive**. Treat every unexpected antivirus alert seriously and verify the file before overriding your security software.

## Official releases

Only download TrueAuto HDR from the official GitHub repository and its GitHub Releases page.

Official release artifacts are accompanied by SHA-256 hashes in `SHA256SUMS.txt`.

On Windows PowerShell you can verify a file with:

```powershell
Get-FileHash .\TrueAutoHDR-1.3.1-Setup.exe -Algorithm SHA256
```

Compare the result with the hash published with the corresponding GitHub release.

## Reporting a suspected false positive

Please include:

- TrueAuto HDR version;
- exact filename detected;
- antivirus product/version;
- exact detection name;
- SHA-256 of the detected file;
- screenshot or detection log if available.

Do not post private account information or unrelated system logs.

## Code signing

TrueAuto HDR's build pipeline supports optional Authenticode signing when a signing certificate is configured on the release machine.

Signing is intended to establish a consistent publisher identity and improve release trust. It does not replace antivirus scanning, source review, or SHA-256 verification.
