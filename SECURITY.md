# Security

Please do not publish exploitable security issues before they can be investigated.

For ordinary bugs, use GitHub Issues. For security-sensitive reports, use GitHub's private vulnerability reporting feature if it is enabled for this repository.

TrueAuto HDR's application updater verifies downloaded update packages with SHA-256. Release/update hosting should be treated as security-sensitive infrastructure.


## Release integrity

Official release builds should publish `SHA256SUMS.txt` alongside Installer and Portable artifacts. Application update manifests also contain the SHA-256 expected by the in-app updater.

If a security product flags an official release, do not assume the detection is harmless. Record the exact filename, version, detection name, and SHA-256 so the release can be compared with the official artifact.

## Signing

The repository includes an optional Authenticode signing hook (`SignRelease.ps1`). Signing credentials are intentionally not stored in the repository. When `TRUEAUTOHDR_CERT_THUMBPRINT` is configured on the release machine, release executables are signed before packaging and the final installer is signed after compilation.
