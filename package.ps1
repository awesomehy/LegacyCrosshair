param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a semantic version such as 0.1.0.' }
& (Join-Path $PSScriptRoot 'build.ps1')
$releaseName = "LegacyCrosshair-v$Version-win-x64"
$releaseDirectory = Join-Path $PSScriptRoot "dist\$releaseName"
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
foreach ($file in @('LegacyCrosshair.exe', 'LegacyCrosshair.exe.config')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin\$file") -Destination (Join-Path $releaseDirectory $file) -Force
}
foreach ($file in @('README.md', 'REFERENCES.md', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $releaseDirectory $file) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'presets') -Destination $releaseDirectory -Recurse -Force
$archivePath = Join-Path $PSScriptRoot "dist\$releaseName.zip"
Compress-Archive -LiteralPath $releaseDirectory -DestinationPath $archivePath -CompressionLevel Optimal -Force
$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
Set-Content -LiteralPath "$archivePath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $releaseName.zip" -Encoding ASCII
Write-Output "Packaged: $archivePath"
Write-Output "SHA256: $($hash.Hash)"
