@echo off
setlocal
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "PS=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
set "TASK=%TEMP%\JapaneseMagnifierUpdater_%RANDOM%_%RANDOM%"
mkdir "%TASK%"
if errorlevel 1 goto :failure
copy /y "%~dp0Update-Magnifier.ps1" "%TASK%\Update-Magnifier.ps1" >nul
if errorlevel 1 goto :failure
rem A detached copy is required: the installed folder cannot be renamed
rem while a batch interpreter or PowerShell has that folder open.
cd /d "%TEMP%"
start "Japanese Magnifier Manual Update" "%PS%" -NoExit -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%TASK%\Update-Magnifier.ps1"
if errorlevel 1 goto :failure
exit /b 0
:failure
echo Update could not be started. Your installed version was not changed.
pause
exit /b 1
