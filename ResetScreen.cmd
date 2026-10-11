@echo off
chcp 65001 >nul
cd /d "%~dp0"
if not exist "ScreenReaderAssist.exe" (
  echo ScreenReaderAssist.exe が見つかりません。同じフォルダーへ配置してください。
  pause
  exit /b 1
)
"ScreenReaderAssist.exe" --reset-display
