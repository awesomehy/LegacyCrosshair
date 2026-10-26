@echo off
cd /d "%~dp0"
if not exist "bin\LegacyCrosshair.exe" powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if not exist "bin\LegacyCrosshair.exe" exit /b 1
start "" "%~dp0bin\LegacyCrosshair.exe"
