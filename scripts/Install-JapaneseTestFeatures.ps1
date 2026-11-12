#Requires -Version 5.1
<#
  GitHub-hosted Windows の日本語 OCR・日本語音声機能を、Microsoft の Windows
  Features on Demand からインストールする。非公式配布物は使わない。
  失敗は成功扱いにせず終了コードでCIへ伝える。
#>
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host '日本語検証用のWindows言語機能を準備します。'
Write-Host ('OS: ' + [Environment]::OSVersion.VersionString)

$capabilities = @(
    'Language.Basic~~~ja-JP~0.0.1.0',
    'Language.Fonts.Jpan~~~und-JPAN~0.0.1.0',
    'Language.OCR~~~ja-JP~0.0.1.0',
    'Language.TextToSpeech~~~ja-JP~0.0.1.0'
)
$failures = @()
foreach ($name in $capabilities) {
    try {
        $capability = Get-WindowsCapability -Online -Name $name -ErrorAction Stop |
            Where-Object { $_.Name -eq $name } |
            Select-Object -First 1
        if (-not $capability) {
            Write-Warning ("Windowsが対応する言語機能を列挙しませんでした: " + $name)
            $failures += $name
            continue
        }
        if ($capability.State -ne 'Installed') {
            Write-Host ("Microsoft Windows Updateから取得中: " + $name)
            $result = Add-WindowsCapability -Online -Name $name -ErrorAction Stop
            if ($result.RestartNeeded) {
                Write-Warning ("再起動が必要と表示されました: " + $name)
            }
        }
        $state = (Get-WindowsCapability -Online -Name $name -ErrorAction Stop |
            Where-Object { $_.Name -eq $name } | Select-Object -First 1).State
        Write-Host ("機能の状態: " + $name + " = " + $state)
        if ($state -ne 'Installed') { $failures += $name }
    }
    catch {
        Write-Warning ("日本語機能の導入失敗: " + $name + " / " + $_.Exception.Message)
        $failures += $name
    }
}

# 特定のFoDが直接取得できない環境では、Microsoft標準の言語導入経路も試す。
if ($failures.Count -gt 0) {
    Write-Warning '個別言語機能が不足しています。Microsoft標準のInstall-Languageを再試行します。'
    try {
        Import-Module LanguagePackManagement -ErrorAction Stop
        Install-Language -Language 'ja-JP' -ErrorAction Stop | Out-String | Write-Host
    } catch {
        Write-Warning ('Install-Language ja-JP 失敗: ' + $_.Exception.Message)
    }
}

$missing = @()
foreach ($name in $capabilities) {
    try {
        $state = (Get-WindowsCapability -Online -Name $name -ErrorAction Stop |
            Where-Object { $_.Name -eq $name } | Select-Object -First 1).State
        Write-Host ("最終状態: " + $name + " = " + $state)
        if ($state -ne 'Installed') { $missing += $name }
    } catch {
        $missing += $name
    }
}

try {
    Set-Culture -CultureInfo 'ja-JP' -ErrorAction Stop
    Write-Host '日本語カルチャーを設定しました（表示言語・システム全体の変更ではありません）。'
} catch {
    Write-Warning ('日本語カルチャーの設定失敗: ' + $_.Exception.Message)
}

if ($missing.Count -gt 0) {
    Write-Error ('日本語のWindows言語機能がインストールできませんでした: ' + ($missing -join ', '))
    exit 1
}
Write-Host '日本語OCRと日本語TextToSpeechのWindows言語機能の導入を確認しました。実際のOCR/音声動作は後続のテストで確認します。'
