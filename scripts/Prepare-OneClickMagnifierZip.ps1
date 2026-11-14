#Requires -Version 7.0
<#
  Pack the latest checked-in three-monitor magnifier alongside the compiled
  ScreenReaderAssist high-accuracy local OCR reader.
  This script runs only in GitHub Actions at build time, never on the user's PC.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PublishDirectory)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$publish=[IO.Path]::GetFullPath($PublishDirectory)
$source=Join-Path $root 'prototypes/Win11MagnifierBase'
$magnifier=Join-Path $publish 'magnifier'
New-Item -ItemType Directory -Path $magnifier -Force | Out-Null

foreach($file in @('DedicatedMagnifier.cs','GlobalOcrSelection.cs','DedicatedMagnifier.ps1','RecognizeSelection.ps1')) {
    $item=Get-ChildItem -LiteralPath $source -File -Recurse -Filter $file |
        Where-Object { $_.Directory.Name -ne 'backup' } | Select-Object -First 1
    if(-not $item) {throw ('Missing magnifier component: '+$file)}
    Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $magnifier $file) -Force
}
foreach($file in @('ScreenReaderAssist.exe','settings.ini','ResetScreen.cmd',
                   'models/ppocrv6_det.onnx','models/ppocrv6_rec.onnx')) {
    if(-not (Test-Path -LiteralPath (Join-Path $publish $file) -PathType Leaf)) {
        throw ('A required offline file is missing: '+$file)
    }
}

# This BAT is ASCII-only so cmd.exe does not depend on the console code page.
# Both programs are explicitly started from the extracted (writable) folder.
$launcher=@'
@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0ScreenReaderAssist.exe" goto :missing
if not exist "%~dp0magnifier\DedicatedMagnifier.ps1" goto :missing
if not exist "%~dp0models\ppocrv6_det.onnx" goto :missing
if not exist "%~dp0models\ppocrv6_rec.onnx" goto :missing
if not exist "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" goto :missing
start "" "%~dp0ScreenReaderAssist.exe"
start "" "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0magnifier\DedicatedMagnifier.ps1"
exit /b 0
:missing
echo Required component missing. Please extract the entire ZIP before starting.
pause
exit /b 1
'@
$ascii=[Text.Encoding]::ASCII
$normalized=$launcher.Replace("`r`n","`n").Replace("`r","`n")
[IO.File]::WriteAllText((Join-Path $publish '拡大鏡実行.bat'),$normalized.Replace("`n","`r`n"),$ascii)

$readme=@'
Windows 11 日本語拡大鏡＆画面読み上げ　試用版
2026年11月15日

【最初に】
1. ZIPを右クリックして「すべて展開」を選んでください。
   ZIPの中から直接BATを実行しないでください。
2. 展開したフォルダー内の「拡大鏡実行.bat」をダブルクリックします。
3. 2つのプログラムが立ち上がります。
   ・3画面用の拡大表示ウィンドウ（拡大鏡）
   ・高精度日本語OCR・読み上げ用の常駐アプリ（通知領域のアイコン）
   読み上げ用アプリの設定画面は起動時に表示されません。
4. 拡大鏡の表示先や倍率は拡大鏡ウィンドウ側で変更できます。

【日本語の読み上げ】
・拡大鏡内の「ダブルクリックの2回目を押したまま左右ドラッグ」で
  音声速度を変更すると、高精度OCR読み上げ側にも反映されます。
  速度の共有はPC内のローカル設定ファイルで行い、通信しません。
  既に読み上げ中の音声には反映されない場合があります。次の読み上げから適用します。
・高精度OCR側の音声速度変更も、拡大鏡側へ反映されます。
・Ctrl＋Alt＋R：高精度日本語OCRの範囲選択（こちらはPaddleOCR v6）。
  ドラッグして範囲を決め、マウスを離すと認識して日本語を読み上げます。
・Altを押しながら右ドラッグ：拡大鏡側の緑枠選択。
  こちらは現在まだWindows標準日本語OCRであり、高精度モデルへの
  一本化は未実装です。Alt＋左クリックや左ドラッグにも対応します。
・読み上げ停止：Alt＋Shift＋S（常駐アプリ側）。
・読み上げの設定：画面右下の通知領域にあるScreen Reader Assistを
  左クリックしてください。
・音声の調整：各ウィンドウの日本語音声設定をご利用ください。

【重要な注意】
・実行中はクラウドOCR・モデルダウンロード・テレメトリーを使用しません。
  OCRモデル2ファイルと必要な.NET実行環境はZIPに同梱済みです。
・PCに日本語SAPI音声がない場合は、読み上げの発声ができません。
  拡大鏡側のAlt＋右ドラッグOCRにはWindows標準の日本語OCRが必要です。
・3画面での位置合わせ、ゲームの画面キャプチャ、実際の日本語音声開始
  までの時間は、Windows 11の利用者実機でまだ検証していません。
・Windowsの拡大率を直接変更する危険な全画面拡大機能は、
  高精度OCRの常駐アプリ側では無効のままにしています。
・Alt＋右／左ドラッグのマウス監視は、他のアプリでのAlt操作と競合
  する場合があります。拡大鏡ウィンドウ内の該当機能をオフにできます。

【終了方法】
・拡大鏡ウィンドウは「×」で閉じてください。
・Screen Reader Assistは通知領域のアイコンを右クリックし、「終了」。
  設定ウィンドウを×で閉じただけでは常駐が継続します。
・画面表示がおかしくなった場合、同梱の「ResetScreen.cmd」で復旧を
  試せます。通常の表示には影響を与えない設計ですが、注意してください。

【作成時の検証】
C#／.NET 10の高精度日本語OCRと同梱モデルをWindows CIで実際に検証。
4種類の日本語画像で全文一致、外向き通信を遮断しても認識に成功。
拡大鏡のC#＋Windows PowerShell 5.1は別のWindows CIでコンパイル検証。
これはWindows 11実機でのマウス操作・音声・3画面の保証ではありません。
'@
[IO.File]::WriteAllText((Join-Path $publish 'README_最初にお読みください.txt'),$readme,[Text.UTF8Encoding]::new($true))

# Remove developer-only debug symbols/import libraries. These are not used
# by the shipped self-contained EXE; retaining the ~96 MB Skia PDB makes an
# accessibility test download unnecessarily large.
foreach($debugName in @('libSkiaSharp.pdb','ScreenReaderAssist.pdb',
                        'onnxruntime.lib','onnxruntime_providers_shared.lib')) {
    $debugPath=Join-Path $publish $debugName
    if(Test-Path -LiteralPath $debugPath -PathType Leaf) {
        Remove-Item -LiteralPath $debugPath -Force
    }
}

$bat=Join-Path $publish '拡大鏡実行.bat'
if(-not(Test-Path -LiteralPath $bat)){throw 'One-click launcher was not created'}
if(-not(Test-Path -LiteralPath (Join-Path $magnifier 'GlobalOcrSelection.cs'))){throw 'Magnifier C# source missing'}
Write-Host "PASS: Created one-click offline magnifier package at $publish"
Write-Host "Entries: Reader EXE + high accuracy Japanese models + original three-monitor magnifier + launcher"
