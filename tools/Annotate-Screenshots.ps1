$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputDirectory = Join-Path $workspaceRoot 'artifacts'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$generator = Join-Path $outputDirectory 'AnnotateScreenshots.exe'
& $compiler /nologo /utf8output /codepage:65001 /target:exe /r:System.Drawing.dll "/out:$generator" (Join-Path $PSScriptRoot 'AnnotateScreenshots.cs')
if ($LASTEXITCODE -ne 0) { throw 'Screenshot annotation compilation failed.' }
& $generator (Join-Path $workspaceRoot 'assets\screenshots')
if ($LASTEXITCODE -ne 0) { throw 'Screenshot annotation or pixel verification failed.' }
