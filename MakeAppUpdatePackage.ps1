param(
    [string]$RepoOwner = "GuruDev-VG",
    [string]$RepoName = "TrueAutoHDR",
    [string]$Version = "1.5.0",
    [ValidateSet("Stable","Canary")][string]$Channel = "Stable"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$channelName = if ($Channel -eq "Canary") { "Canary" } else { "Stable" }
$manifestName = if ($Channel -eq "Canary") { "canary.json" } else { "stable.json" }
$out = Join-Path $root ("UpdatePackages\" + $channelName)
$payload = Join-Path $root ("update-payload-" + $channelName.ToLowerInvariant())
$updaterOut = Join-Path $root ("publish-updater-" + $channelName.ToLowerInvariant())
$log = Join-Path $root ("MakeAppUpdatePackage-" + $channelName.ToLowerInvariant() + ".log")

function Stop-WithError([string]$Message) {
    Write-Host ""; Write-Host "UPDATE PACKAGE BUILD FAILED" -ForegroundColor Red; Write-Host $Message -ForegroundColor Red
    throw $Message
}

try {
    if (Test-Path $log) { Remove-Item $log -Force }
    Start-Transcript -Path $log -Force | Out-Null
    if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) { Stop-WithError ".NET SDK was not found in PATH." }

    # Isolation rule: only this channel's output directory is cleaned. A Canary
    # build never touches UpdatePackages\Stable or stable.json, and vice versa.
    foreach ($folder in @($payload, $out, $updaterOut)) {
        if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    $mainProject = Join-Path $root "AutoHDR.csproj"
    $updaterProject = Join-Path $root "Updater\TrueAutoHDR.Updater.csproj"

    $channelDefine = if ($Channel -eq "Canary") { "CANARY" } else { "STABLE" }

    Write-Host "[1/7] Building updater ($Channel)..."
    & dotnet publish $updaterProject -c Release -r win-x64 --self-contained true `
        "-p:Version=$Version" "-p:DefineConstants=$channelDefine" `
        "-p:PublishSingleFile=true" "-p:DebugType=None" "-p:DebugSymbols=false" -o $updaterOut
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Updater build failed." }

    Write-Host "[2/7] Building TrueAuto HDR $Version..."
    & dotnet publish $mainProject -c Release -r win-x64 --self-contained true `
        "-p:Version=$Version" "-p:DefineConstants=$channelDefine" `
        "-p:PublishSingleFile=true" "-p:PublishReadyToRun=true" "-p:DebugType=None" "-p:DebugSymbols=false" -o $payload
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Main application build failed." }

    Copy-Item (Join-Path $updaterOut "TrueAutoHDR.Updater.exe") (Join-Path $payload "TrueAutoHDR.Updater.exe") -Force
    foreach ($required in @("TrueAutoHDR.exe","TrueAutoHDR.Updater.exe","Database\native_hdr_database.json","Database\community_hdr_names.json")) {
        if (-not (Test-Path (Join-Path $payload $required))) { Stop-WithError "Required payload file missing: $required" }
    }

    Write-Host "[3/7] Optional signing..."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "SignRelease.ps1") -Path $payload
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Code signing step failed." }

    Write-Host "[4/7] Self-test..."
    $selfTest = Start-Process -FilePath (Join-Path $payload "TrueAutoHDR.exe") -ArgumentList "--self-test" -WorkingDirectory $payload -Wait -PassThru
    if ($selfTest.ExitCode -ne 0) { Stop-WithError "Self-test failed with exit code $($selfTest.ExitCode)." }

    Write-Host "[5/7] Creating package..."
    $zipName = "TrueAutoHDR-update-$Version.zip"
    $zipPath = Join-Path $out $zipName
    Compress-Archive -Path (Join-Path $payload "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

    Write-Host "[6/7] Hashing package..."
    $sha256 = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$sha256  $zipName" | Set-Content -Path (Join-Path $out "$zipName.sha256") -Encoding ASCII

    Write-Host "[7/7] Writing isolated $Channel manifest..."
    $tag = "v$Version"
    $packageUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$tag/$zipName"
    $manifest = [ordered]@{
        version = $Version
        releaseType = $Channel
        packageUrl = $packageUrl
        sha256 = $sha256
        notes = if ($Channel -eq "Canary") { "TrueAuto HDR $Version Canary: experimental feature channel." } else { "TrueAuto HDR $Version Stable: HDR10+ Gaming, display recovery, redesigned Game Manager, and Steam artwork cache." }
    }
    $manifestPath = Join-Path $out $manifestName
    $manifest | ConvertTo-Json | Out-File -FilePath $manifestPath -Encoding utf8 -Force

    Write-Host ""; Write-Host "$Channel update package complete." -ForegroundColor Green
    Write-Host "Package:  $zipPath"
    Write-Host "Manifest: $manifestPath"
    Write-Host "SHA-256:  $sha256"
    Stop-Transcript | Out-Null
    exit 0
}
catch {
    try { Stop-Transcript | Out-Null } catch {}
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
