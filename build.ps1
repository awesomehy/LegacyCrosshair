param([switch]$Test, [switch]$SmokeTest)
$ErrorActionPreference = 'Stop'
$workspaceRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework C# compiler was not found.' }
$outputDirectory = Join-Path $workspaceRoot 'bin'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $workspaceRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll')
$icon = Join-Path $workspaceRoot 'assets\LegacyCrosshair.ico'
$iconOptions = @("/win32icon:$icon", "/resource:$icon,LegacyCrosshair.App.ico")
& $compiler /nologo /utf8output /codepage:65001 /target:winexe /platform:x64 /optimize+ "/win32manifest:$workspaceRoot\app.manifest" "/out:$outputDirectory\LegacyCrosshair.exe" $iconOptions $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item -LiteralPath (Join-Path $workspaceRoot 'app.config') -Destination (Join-Path $outputDirectory 'LegacyCrosshair.exe.config') -Force
Write-Output "Built: $outputDirectory\LegacyCrosshair.exe"
if ($Test) {
    & $compiler /nologo /utf8output /codepage:65001 /target:exe /platform:x64 /optimize+ "/out:$outputDirectory\LegacyCrosshair.Tests.exe" $references (Join-Path $workspaceRoot 'src\Core.cs') (Join-Path $workspaceRoot 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & (Join-Path $outputDirectory 'LegacyCrosshair.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
}
if ($SmokeTest) {
    $uiSources = @($sources | Where-Object { [System.IO.Path]::GetFileName($_) -ne 'Program.cs' })
    & $compiler /nologo /utf8output /codepage:65001 /target:exe /define:UI_TEST /platform:x64 /optimize+ "/win32manifest:$workspaceRoot\app.manifest" "/out:$outputDirectory\LegacyCrosshair.UiTests.exe" $iconOptions $references $uiSources (Join-Path $workspaceRoot 'tests\UiSmokeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke test compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $workspaceRoot 'app.config') -Destination (Join-Path $outputDirectory 'LegacyCrosshair.UiTests.exe.config') -Force
    & (Join-Path $outputDirectory 'LegacyCrosshair.UiTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke tests failed.' }
}
