@echo off
setlocal
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Magnifier-CDrive.ps1"
if errorlevel 1 (
  echo Installation failed. Please inspect the displayed error.
  pause
  exit /b 1
)
echo Installation completed. Open the Japanese magnifier shortcut on your desktop.
pause
