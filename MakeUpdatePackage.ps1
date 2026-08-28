param(
  [Parameter(Mandatory=$true)][string]$Version,
  [Parameter(Mandatory=$true)][string]$PublishFolder,
  [Parameter(Mandatory=$true)][string]$PackageUrl,
  [ValidateSet("Stable","Hotfix","Canary")][string]$ReleaseType = "Stable",
  [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "UpdatePackages"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$zip = Join-Path $out "TrueAutoHDR-update-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Compress-Archive -Path (Join-Path $PublishFolder "*") -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
  version = $Version
  releaseType = $ReleaseType
  packageUrl = $PackageUrl
  sha256 = $hash
  notes = $Notes
}
$manifestPath = Join-Path $out (($ReleaseType.ToLowerInvariant()) + "-manifest.json")
$manifest | ConvertTo-Json | Set-Content -Encoding UTF8 $manifestPath

Write-Host "Package: $zip"
Write-Host "Manifest: $manifestPath"
Write-Host "SHA256: $hash"
