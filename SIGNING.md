# Code signing TrueAuto HDR

TrueAuto HDR can optionally Authenticode-sign its Windows release executables.

Signing is **not required to build the project**. If no certificate is configured, the build scripts print that signing was skipped and continue normally.

## What gets signed

When signing is enabled:

- `TrueAutoHDR.exe`
- `TrueAutoHDR.Updater.exe`
- the final Inno Setup installer

Portable/application payload binaries are signed **before** ZIP packaging so the signatures survive inside the archive.

## Configuration

The repository does not contain signing keys, passwords, certificate files, or other credentials.

`SignRelease.ps1` reads the certificate thumbprint from:

```text
TRUEAUTOHDR_CERT_THUMBPRINT
```

The certificate must already be available to the Windows signing environment and `signtool.exe` must be installed.

Example for the current PowerShell session:

```powershell
$env:TRUEAUTOHDR_CERT_THUMBPRINT = "YOUR_CERTIFICATE_THUMBPRINT"
```

Then run the normal builders. Each signature is verified immediately after signing.

## Important

Never commit a `.pfx`, private key, certificate password, token, or signing-service credential to the repository.

A signature proves which signing identity produced a binary and whether the binary was modified afterward. It does not by itself prove that software is safe, so release hashes, antivirus scanning, source review, and normal testing still matter.
