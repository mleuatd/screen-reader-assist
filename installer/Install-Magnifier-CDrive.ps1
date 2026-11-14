#Requires -Version 5.1
<#
  Initial install uses the exact same verified updater path as future updates.
  The user must explicitly run the BAT; there is no app runtime networking.
#>
$ErrorActionPreference = 'Stop'
$updater=Join-Path $PSScriptRoot 'Update-Magnifier.ps1'
if(-not (Test-Path -LiteralPath $updater -PathType Leaf)) {
    throw '最新版に更新するためのファイルがありません。ZIPをすべて展開してください。'
}
& $updater
if($LASTEXITCODE -ne 0) {exit $LASTEXITCODE}
