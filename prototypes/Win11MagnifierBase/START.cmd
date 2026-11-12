@echo off
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Win11MagnifierBase.ps1"
if errorlevel 1 (
  echo Unable to launch the app. Please keep this window open and copy the error.
  pause
)