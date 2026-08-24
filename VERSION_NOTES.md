# Version baseline

This repository tree is the definitive **TrueAuto HDR 1.3.1** source.

1.3.1 adds release-trust infrastructure:
- SHA-256 verification files for public release artifacts
- SHA-256 sidecar for in-app update packages
- optional Authenticode signing hooks
- antivirus / false-positive documentation and reporting guidance
- expanded release integrity checklist

Runtime HDR automation remains based on the 1.3.0 feature set. These changes add no new background polling or idle resource usage.
