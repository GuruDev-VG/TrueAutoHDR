param(
    [string]$ReleaseRoot = (Join-Path $PSScriptRoot "release")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ReleaseRoot)) {
    throw "Release folder not found: $ReleaseRoot"
}

$files = @()

$portable = Join-Path $ReleaseRoot "Portable"
$installer = Join-Path $ReleaseRoot "Installer"

if (Test-Path $portable) {
    $files += Get-ChildItem -Path $portable -File -Filter "*.zip"
}

if (Test-Path $installer) {
    $files += Get-ChildItem -Path $installer -File -Filter "*.exe"
}

$files = $files | Sort-Object FullName

if ($files.Count -eq 0) {
    throw "No release EXE/ZIP artifacts were found."
}

$out = Join-Path $ReleaseRoot "SHA256SUMS.txt"
$lines = foreach ($file in $files) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    # Compatible with the Windows PowerShell 5.1 / .NET Framework shipped with Windows.
    $rootFull = [IO.Path]::GetFullPath($ReleaseRoot).TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar
    $rootUri = New-Object System.Uri($rootFull)
    $fileUri = New-Object System.Uri([IO.Path]::GetFullPath($file.FullName))
    $relative = [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fileUri).ToString())
    "$hash  $relative"
}

$lines | Set-Content -Path $out -Encoding ASCII

Write-Host ""
Write-Host "SHA-256 verification file:"
Write-Host "  $out"
Write-Host ""
$lines | ForEach-Object { Write-Host $_ }
