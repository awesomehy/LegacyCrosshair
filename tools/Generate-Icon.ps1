$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputDirectory = Join-Path $workspaceRoot 'artifacts'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$generator = Join-Path $outputDirectory 'GenerateIcon.exe'
& $compiler /nologo /target:exe /r:System.Drawing.dll "/out:$generator" (Join-Path $PSScriptRoot 'GenerateIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon generator compilation failed.' }
& $generator (Join-Path $workspaceRoot 'assets')
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
