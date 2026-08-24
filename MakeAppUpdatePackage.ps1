param(
    [string]$RepoOwner = "GuruDev-VG",
    [string]$RepoName = "TrueAutoHDR",
    [string]$Version = "1.3.2"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$payload = Join-Path $root "update-payload"
$out = Join-Path $root "UpdatePackages"
$updaterOut = Join-Path $root "publish-updater"
$log = Join-Path $root "MakeAppUpdatePackage.log"

function Stop-WithError([string]$Message) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "UPDATE PACKAGE BUILD FAILED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host $Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Log:"
    Write-Host "  $log"
    throw $Message
}

try {
    if (Test-Path $log) { Remove-Item $log -Force }
    Start-Transcript -Path $log -Force | Out-Null

    Write-Host "========================================"
    Write-Host " TrueAuto HDR $Version App Update Package"
    Write-Host "========================================"
    Write-Host ""

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        Stop-WithError ".NET SDK was not found in PATH."
    }

    Write-Host "Using .NET:"
    & dotnet --version
    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "dotnet --version failed."
    }

    $mainProject = Join-Path $root "AutoHDR.csproj"
    $updaterProject = Join-Path $root "Updater\TrueAutoHDR.Updater.csproj"

    if (-not (Test-Path $mainProject)) {
        Stop-WithError "AutoHDR.csproj was not found: $mainProject"
    }
    if (-not (Test-Path $updaterProject)) {
        Stop-WithError "Updater project was not found: $updaterProject"
    }

    foreach ($folder in @($payload, $out, $updaterOut)) {
        if (Test-Path $folder) {
            Remove-Item $folder -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    Write-Host ""
    Write-Host "[1/6] Building self-contained updater..."
    & dotnet publish $updaterProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        "-p:PublishSingleFile=true" `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false" `
        -o $updaterOut

    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "Updater build failed with exit code $LASTEXITCODE."
    }

    $updaterExe = Join-Path $updaterOut "TrueAutoHDR.Updater.exe"
    if (-not (Test-Path $updaterExe)) {
        Stop-WithError "Updater build completed but TrueAutoHDR.Updater.exe is missing."
    }

    Write-Host ""
    Write-Host "[2/6] Building TrueAuto HDR $Version payload..."
    & dotnet publish $mainProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        "-p:PublishSingleFile=true" `
        "-p:PublishReadyToRun=true" `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false" `
        -o $payload

    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "Main application build failed with exit code $LASTEXITCODE."
    }

    $mainExe = Join-Path $payload "TrueAutoHDR.exe"
    if (-not (Test-Path $mainExe)) {
        Stop-WithError "Main build completed but TrueAutoHDR.exe is missing."
    }

    Write-Host ""
    Write-Host "[3/6] Adding updater to payload..."
    Copy-Item $updaterExe (Join-Path $payload "TrueAutoHDR.Updater.exe") -Force

    $requiredFiles = @(
        (Join-Path $payload "TrueAutoHDR.exe"),
        (Join-Path $payload "TrueAutoHDR.Updater.exe"),
        (Join-Path $payload "Database\native_hdr_database.json"),
        (Join-Path $payload "Database\community_hdr_names.json")
    )

    foreach ($required in $requiredFiles) {
        if (-not (Test-Path $required)) {
            Stop-WithError "Required update payload file is missing: $required"
        }
    }

    Write-Host ""
    Write-Host "[4/7] Optional code signing..."
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "SignRelease.ps1") -Path $payload
    if ($LASTEXITCODE -ne 0) { Stop-WithError "Code signing step failed." }

    Write-Host ""
    Write-Host "[5/7] Running built-in self-test..."
    $selfTest = Start-Process `
        -FilePath $mainExe `
        -ArgumentList "--self-test" `
        -WorkingDirectory $payload `
        -Wait `
        -PassThru

    if ($selfTest.ExitCode -ne 0) {
        Stop-WithError "TrueAutoHDR.exe --self-test failed with exit code $($selfTest.ExitCode)."
    }

    Write-Host "Self-test passed."

    Write-Host ""
    Write-Host "[6/7] Creating update ZIP..."
    $zipName = "TrueAutoHDR-update-$Version.zip"
    $zipPath = Join-Path $out $zipName

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive `
        -Path (Join-Path $payload "*") `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal `
        -Force

    if (-not (Test-Path $zipPath)) {
        Stop-WithError "Compress-Archive did not create the update ZIP."
    }

    Write-Host ""
    Write-Host "[7/7] Creating GitHub Stable manifest..."
    $sha256 = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $shaPath = Join-Path $out "$zipName.sha256"
    "$sha256  $zipName" | Set-Content -Path $shaPath -Encoding ASCII
    $tag = "v$Version"
    $packageUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$tag/$zipName"

    $manifest = [ordered]@{
        version = $Version
        releaseType = "Stable"
        packageUrl = $packageUrl
        sha256 = $sha256
        notes = "TrueAuto HDR 1.3.2: close-window choice dialog with optional remembered behavior."
    }

    $manifestPath = Join-Path $out "stable.json"
    $manifest | ConvertTo-Json | Out-File `
        -FilePath $manifestPath `
        -Encoding utf8 `
        -Force

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "UPDATE PACKAGE BUILD COMPLETE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "GitHub Release asset:"
    Write-Host "  $zipPath"
    Write-Host ""
    Write-Host "Repository manifest to upload AFTER the release asset:"
    Write-Host "  $manifestPath"
    Write-Host ""
    Write-Host "Package URL:"
    Write-Host "  $packageUrl"
    Write-Host ""
    Write-Host "SHA-256:"
    Write-Host "  $sha256"
    Write-Host ""
    Write-Host "Full build log:"
    Write-Host "  $log"

    Stop-Transcript | Out-Null
    exit 0
}
catch {
    try { Stop-Transcript | Out-Null } catch {}
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "The window will remain open when launched with BuildStableTestUpdate.bat."
    exit 1
}
