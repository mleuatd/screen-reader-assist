#Requires -Version 5.1
<#
  64-bit Windows 11 installer. Network access ONLY to download a signed-by-origin
  public GitHub release ZIP during installation; the installed OCR/reader do not
  contact the network while running. Installs to the real 64-bit Program Files.
#>
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$release = 'https://github.com/mleuatd/screen-reader-assist/releases/download/tryout-20261115-hud/ScreenReaderAssist-win-x64.zip'
$installName = '日本語拡大鏡'
$programFiles = [Environment]::GetFolderPath('ProgramFiles')
$destination = Join-Path $programFiles $installName

function Require-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { return }
    Write-Host 'Program Filesへのインストールには管理者権限が必要です。Windowsの確認画面を表示します。'
    $ps = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $quoted = '"' + $PSCommandPath.Replace('"','') + '"'
    $child = Start-Process -FilePath $ps -Verb RunAs -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File ' + $quoted) -PassThru -Wait
    exit $child.ExitCode
}

Require-Administrator

if (-not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess) {
    throw 'この配布物は64ビットWindows用です。64ビットPowerShellから実行してください。'
}
if ($programFiles -notmatch '^[Cc]:\\') {
    throw 'CドライブのProgram Filesが見つかりません。想定外の場所には自動インストールしません。'
}

$stamp = [guid]::NewGuid().ToString('N')
$work = Join-Path ([IO.Path]::GetTempPath()) ('JapaneseMagnifierInstall_' + $stamp)
$zip = Join-Path $work 'package.zip'
$unpacked = Join-Path $work 'unpacked'
$staging = Join-Path $programFiles ('日本語拡大鏡_準備中_' + $stamp)
$backup = Join-Path $programFiles ('日本語拡大鏡_旧版_' + $stamp)
$oldRenamed = $false
$newInstalled = $false

try {
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    Write-Host ('インストール先：' + $destination)
    Write-Host 'GitHubの公開済みWindows 11用ZIPをダウンロードしています。'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $release -OutFile $zip -TimeoutSec 360 -MaximumRedirection 6
    if ((Get-Item -LiteralPath $zip).Length -lt 30000000) {
        throw 'ダウンロードしたZIPが不完全です。再実行してください。'
    }
    Write-Host ('取得ZIPのSHA256：' + (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)

    Expand-Archive -LiteralPath $zip -DestinationPath $unpacked -Force
    foreach ($relative in @(
        'ScreenReaderAssist.exe','拡大鏡実行.bat',
        'magnifier\DedicatedMagnifier.ps1',
        'magnifier\DedicatedMagnifier.cs',
        'magnifier\GlobalOcrSelection.cs',
        'models\ppocrv6_det.onnx','models\ppocrv6_rec.onnx',
        'ResetScreen.cmd','settings.ini'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $unpacked $relative) -PathType Leaf)) {
            throw ('ZIPの必須ファイルが欠落しています：' + $relative)
        }
    }
    Write-Host '日本語モデル・拡大鏡・読み上げアプリを確認しました。'

    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -Path (Join-Path $unpacked '*') -Destination $staging -Recurse -Force
    if (Test-Path -LiteralPath $destination) {
        Write-Host '旧版を安全のため退避します。'
        Rename-Item -LiteralPath $destination -NewName (Split-Path -Leaf $backup)
        $oldRenamed = $true
    }
    Rename-Item -LiteralPath $staging -NewName $installName
    $newInstalled = $true

    # The installed exe is always launched unelevated via a shortcut.
    # Public Desktop shortcut supports standard-user accounts too.
    $wsh = New-Object -ComObject WScript.Shell
    $desktop = Join-Path $env:PUBLIC 'Desktop'
    if(-not (Test-Path $desktop)) { New-Item -ItemType Directory -Force -Path $desktop | Out-Null }
    $shortcut = $wsh.CreateShortcut((Join-Path $desktop '日本語拡大鏡.lnk'))
    $shortcut.TargetPath = Join-Path $destination '拡大鏡実行.bat'
    $shortcut.WorkingDirectory = $destination
    $shortcut.Description = '3画面拡大鏡と完全オフライン日本語OCRを起動'
    $shortcut.Save()
    Write-Host ('インストールが完了しました：' + $destination)
    Write-Host 'デスクトップの「日本語拡大鏡」をダブルクリックして起動してください。'
    Write-Host '設定はユーザーのAppDataに保存します。以後のOCRと音声読み上げにネット接続は不要です。'
    if($oldRenamed) {
        Write-Host ('旧版のバックアップ：' + $backup)
        Write-Host '新しい版が問題なく動くと確認できるまでバックアップを保存します。'
    }
}
catch {
    Write-Host ('インストールエラー：' + $_.Exception.Message) -ForegroundColor Red
    if ($oldRenamed -and -not $newInstalled -and
        -not (Test-Path -LiteralPath $destination) -and
        (Test-Path -LiteralPath $backup)) {
        Rename-Item -LiteralPath $backup -NewName $installName
        Write-Host '以前の版を復旧しました。'
    }
    throw
}
finally {
    if(Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Force -Recurse -ErrorAction SilentlyContinue
    }
    if(Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Force -Recurse -ErrorAction SilentlyContinue
    }
}
