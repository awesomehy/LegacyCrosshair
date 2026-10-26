param([string]$Label = 'current', [string]$SourceDirectory = '', [ValidateRange(1, 120)][int]$SampleSeconds = 10)
$ErrorActionPreference = 'Stop'
if ($Label -notmatch '^[a-zA-Z0-9-]+$') { throw 'Label must contain only letters, digits or hyphens.' }
$workspaceRoot = Split-Path -Parent $PSScriptRoot
if (!$SourceDirectory) { $SourceDirectory = Join-Path $workspaceRoot 'src' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputPath = Join-Path $workspaceRoot 'bin\LegacyCrosshair.Performance.exe'
$sources = @(Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.cs' | Where-Object Name -ne 'Program.cs' | ForEach-Object FullName)
& $compiler /nologo /utf8output /codepage:65001 /target:exe /define:UI_TEST /platform:x64 /optimize+ "/win32manifest:$workspaceRoot\app.manifest" "/resource:$workspaceRoot\assets\LegacyCrosshair.ico,LegacyCrosshair.App.ico" "/out:$outputPath" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll $sources (Join-Path $PSScriptRoot 'PerformanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Performance benchmark compilation failed.' }
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'app.config') -Destination "$outputPath.config" -Force
& $outputPath $Label $SampleSeconds
if ($LASTEXITCODE -ne 0) { throw 'Performance benchmark failed.' }
