param(
    [string]$Path,
    [string]$CertificateThumbprint = $env:TRUEAUTOHDR_CERT_THUMBPRINT,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Path)) {
    throw "A file or directory path is required."
}

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    Write-Host "Signing skipped: TRUEAUTOHDR_CERT_THUMBPRINT is not configured."
    exit 0
}

$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if ($null -eq $signtool) {
    throw "signtool.exe was not found. Install the Windows SDK or configure signing separately."
}

$targets = @()
if (Test-Path $Path -PathType Leaf) {
    $targets += Get-Item $Path
}
elseif (Test-Path $Path -PathType Container) {
    $targets += Get-ChildItem $Path -Recurse -File |
        Where-Object { $_.Extension -eq ".exe" }
}
else {
    throw "Signing path does not exist: $Path"
}

foreach ($target in $targets) {
    Write-Host "Signing $($target.FullName)"
    & signtool.exe sign `
        /sha1 $CertificateThumbprint `
        /fd SHA256 `
        /tr $TimestampUrl `
        /td SHA256 `
        $target.FullName

    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for $($target.FullName)"
    }

    & signtool.exe verify /pa /v $target.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Signature verification failed for $($target.FullName)"
    }
}
