# TrueAuto HDR 1.2.2

Developed by **VG Prod.**

## Build hotfix

Fixes C# compiler error CS8754 in `AppUpdateService.NormalizeHash`.

The invalid target-typed expression:

```csharp
new(char[])
```

has been replaced with explicit string construction:

```csharp
new string(char[])
```

No update-channel, HDR-detection, updater, or UI behavior changed.
