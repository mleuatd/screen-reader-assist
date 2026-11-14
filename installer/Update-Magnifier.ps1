#Requires -Version 5.1
<#
  User-initiated manual updater. This script is NOT called by the application.
  Network access is exclusively for retrieving GitHub's verified release ZIP.
  The running magnifier/OCR application never accesses the network.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$repo = 'mleuatd/screen-reader-assist'
$tag = 'tryout-latest'
$installName = '日本語拡大鏡'
$programFiles = [Environment]::GetFolderPath('ProgramFiles')
$destination = Join-Path $programFiles $installName
$expectedAsset = 'ScreenReaderAssist-win-x64.zip'
$assetPrefix = 'https://github.com/' + $repo + '/releases/download/' + $tag + '/'

function Require-Administrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $admin = New-Object Security.Principal.WindowsPrincipal($id)
    if ($admin.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { return }
    Write-Host '更新するため、Windowsの管理者確認画面を開きます。'
    $ps = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not [Environment]::Is64BitProcess -and [Environment]::Is64BitOperatingSystem) {
        $ps = Join-Path $env:WINDIR 'Sysnative\WindowsPowerShell\v1.0\powershell.exe'
    }
    $quoted = '"' + $PSCommandPath.Replace('"', '') + '"'
    $child = Start-Process -FilePath $ps -Verb RunAs -ArgumentList (
        '-NoLogo -NoProfile -ExecutionPolicy Bypass -File ' + $quoted
    ) -PassThru -Wait
    exit $child.ExitCode
}

function Assert-ProgramsClosed {
    if (-not (Test-Path -LiteralPath $destination)) { return }
    try {
        $processes = Get-CimInstance Win32_Process -Filter (
            "Name='ScreenReaderAssist.exe' OR Name='powershell.exe' OR Name='pwsh.exe'"
        )
        foreach ($proc in $processes) {
            $path = [string]$proc.ExecutablePath
            $command = [string]$proc.CommandLine
            $reader = $path.StartsWith(($destination + '\'), [StringComparison]::OrdinalIgnoreCase)
            $viewer = $command.IndexOf('DedicatedMagnifier.ps1', [StringComparison]::OrdinalIgnoreCase) -ge 0 -and
                $command.IndexOf($destination, [StringComparison]::OrdinalIgnoreCase) -ge 0
            if ($reader -or $viewer) {
                throw '拡大鏡または読み上げアプリが起動しています。両方を終了してから「最新版に更新」を再実行してください。'
            }
        }
    } catch [Microsoft.Management.Infrastructure.CimException] {
        # Last-resort safe check if CIM is unavailable.
        if (Get-Process -Name ScreenReaderAssist -ErrorAction SilentlyContinue) {
            throw '読み上げアプリを終了してから更新してください。'
        }
    }
}

function Get-ReleaseAsset($release, [string]$name) {
    $matches = @($release.assets | Where-Object { $_.name -eq $name -and $_.state -eq 'uploaded' })
    if ($matches.Count -ne 1) { throw ('GitHubに必要な更新ファイルがありません：' + $name) }
    $asset = $matches[0]
    if (-not ([string]$asset.browser_download_url).StartsWith(
        ($assetPrefix + $name), [StringComparison]::OrdinalIgnoreCase)) {
        throw ('GitHubの配布先が想定外です：' + $name)
    }
    return $asset
}

function Download-ReleaseAsset($release, [string]$name, [string]$to) {
    $asset = Get-ReleaseAsset $release $name
    Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $to -TimeoutSec 360
    $actualSize = (Get-Item -LiteralPath $to).Length
    if ($actualSize -ne [long]$asset.size) {
        throw ('ダウンロードしたファイルのサイズが一致しません：' + $name)
    }
}

function Make-Shortcuts {
    if (-not (Test-Path -LiteralPath (Join-Path $destination '最新版に更新.bat'))) {
        throw '更新用バッチが配布物にありません。'
    }
    $desktop = Join-Path $env:PUBLIC 'Desktop'
    if (-not (Test-Path -LiteralPath $desktop)) {
        New-Item -ItemType Directory -Path $desktop -Force | Out-Null
    }
    $wsh = New-Object -ComObject WScript.Shell
    $launch = $wsh.CreateShortcut((Join-Path $desktop '日本語拡大鏡.lnk'))
    $launch.TargetPath = Join-Path $destination '拡大鏡実行.bat'
    $launch.WorkingDirectory = $destination
    $launch.Description = '3画面拡大鏡と日本語OCRを起動（オフライン）'
    $launch.Save()
    $update = $wsh.CreateShortcut((Join-Path $desktop '日本語拡大鏡を更新.lnk'))
    $update.TargetPath = Join-Path $destination '最新版に更新.bat'
    $update.WorkingDirectory = $destination
    $update.Description = 'GitHubの検証済み最新版を手動でインストール'
    $update.Save()
}

Require-Administrator
if (-not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess) {
    throw '64ビットWindows 11と64ビットPowerShellが必要です。'
}
if (-not $programFiles.StartsWith('C:\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'CドライブのProgram Filesが見つかりません。Dドライブは使用しません。'
}
Assert-ProgramsClosed

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$headers = @{
    'User-Agent' = 'JapaneseMagnifier-ManualUpdater'
    'Accept' = 'application/vnd.github+json'
}
Write-Host 'GitHubの検証済み最新版を確認しています。'
$api = 'https://api.github.com/repos/' + $repo + '/releases/tags/' + $tag
$release = Invoke-RestMethod -UseBasicParsing -Uri $api -Headers $headers -TimeoutSec 45
if ($release.draft -or $release.tag_name -ne $tag) {
    throw '最新版の公開情報を確認できません。既存のアプリは変更しません。'
}

$id = [guid]::NewGuid().ToString('N')
$work = Join-Path ([IO.Path]::GetTempPath()) ('JapaneseMagnifierUpdate_' + $id)
$archive = Join-Path $work 'package.zip'
$checksumFile = Join-Path $work 'CHECKSUMS.sha256'
$versionFile = Join-Path $work 'latest-version.txt'
$unpacked = Join-Path $work 'unpacked'
$staging = Join-Path $programFiles ('日本語拡大鏡_準備中_' + $id)
$backup = Join-Path $programFiles ('日本語拡大鏡_旧版_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + $id.Substring(0,6))
$oldRenamed = $false
$newInstalled = $false
try {
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    Download-ReleaseAsset $release 'latest-version.txt' $versionFile
    $newVersion = (Get-Content -LiteralPath $versionFile -Raw -Encoding ASCII).Trim()
    if ($newVersion -notmatch '^[0-9a-fA-F]{40}$') {
        throw '最新版のビルド識別情報が正しくありません。'
    }
    $installedVersionPath = Join-Path $destination 'version.txt'
    if (Test-Path -LiteralPath $installedVersionPath) {
        $installedVersion = (Get-Content -LiteralPath $installedVersionPath -Raw -Encoding ASCII).Trim()
        if ($installedVersion -eq $newVersion) {
            Make-Shortcuts
            Write-Host '既に最新版です。ダウンロードは不要でした。'
            Write-Host ('バージョン：' + $newVersion.Substring(0,12))
            return
        }
    }
    Write-Host '新しい版を確認しました。更新ZIPをダウンロードします。'
    Download-ReleaseAsset $release 'CHECKSUMS.sha256' $checksumFile
    Download-ReleaseAsset $release $expectedAsset $archive

    $checks = @(Get-Content -LiteralPath $checksumFile -Encoding ASCII |
        Where-Object { $_ -match ('(?i)^([0-9a-f]{64})\s+\*?' +
            [regex]::Escape($expectedAsset) + '$') })
    if ($checks.Count -ne 1) { throw '公開済みZIPのSHA-256検証情報がありません。' }
    $expectedHash = [regex]::Match($checks[0], '(?i)^[0-9a-f]{64}').Value
    $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    if ($expectedHash -ne $actualHash) {
        throw 'ZIPのSHA-256が一致しません。更新を中止し、既存版を維持します。'
    }
    Write-Host 'GitHub配布ZIPのSHA-256検証に成功しました。'
    Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
    foreach ($required in @(
        'ScreenReaderAssist.exe','拡大鏡実行.bat','最新版に更新.bat',
        'Update-Magnifier.ps1','models\ppocrv6_det.onnx',
        'models\ppocrv6_rec.onnx','magnifier\DedicatedMagnifier.ps1',
        'magnifier\DedicatedMagnifier.cs','magnifier\GlobalOcrSelection.cs',
        'ResetScreen.cmd','settings.ini','version.txt'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $unpacked $required) -PathType Leaf)) {
            throw ('必要なファイルが見つかりません：' + $required)
        }
    }
    $bundledVersion = (Get-Content -LiteralPath (Join-Path $unpacked 'version.txt') -Raw -Encoding ASCII).Trim()
    if ($bundledVersion -ne $newVersion) { throw 'ZIP内のバージョン情報が一致しません。' }

    Assert-ProgramsClosed
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    Copy-Item -Path (Join-Path $unpacked '*') -Destination $staging -Recurse -Force

    if (Test-Path -LiteralPath $destination) {
        Rename-Item -LiteralPath $destination -NewName (Split-Path -Leaf $backup)
        $oldRenamed = $true
    }
    Rename-Item -LiteralPath $staging -NewName $installName
    $newInstalled = $true
    Make-Shortcuts
    Write-Host '更新が完了しました。デスクトップの「日本語拡大鏡」から起動できます。'
    Write-Host ('最新版：' + $newVersion.Substring(0,12))
    if ($oldRenamed) {
        Write-Host ('万一に備えた旧版バックアップ：' + $backup)
    }
    Write-Host '拡大鏡・日本語OCR・音声読み上げを使うときはネット接続不要です。'
} catch {
    Write-Host ('更新失敗：' + $_.Exception.Message) -ForegroundColor Red
    if ($newInstalled -and (Test-Path -LiteralPath $destination) -and $oldRenamed) {
        Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction Stop
        $newInstalled = $false
    }
    if ($oldRenamed -and -not (Test-Path -LiteralPath $destination) -and
        (Test-Path -LiteralPath $backup)) {
        Rename-Item -LiteralPath $backup -NewName $installName
        Write-Host '旧版に戻しました。'
    }
    throw
} finally {
    if (Test-Path -LiteralPath $staging) {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
