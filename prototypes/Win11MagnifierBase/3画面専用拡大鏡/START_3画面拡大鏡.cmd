@echo off
cd /d "%~dp0"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0DedicatedMagnifier.ps1"
if errorlevel 1 (
  echo Could not start the 3-monitor magnifier.
  echo Please review the error message on screen.
  pause
)