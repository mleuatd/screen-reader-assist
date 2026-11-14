#Requires -Version 5.1
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$installer=Join-Path $root 'installer/Install-Magnifier-CDrive.ps1'
$updater=Join-Path $root 'installer/Update-Magnifier.ps1'
$installerBat=Join-Path $root 'installer/日本語拡大鏡_インストール.bat'
$updaterBat=Join-Path $root 'installer/最新版に更新.bat'
foreach($file in @($installer,$updater,$installerBat,$updaterBat)) {
    if(-not(Test-Path -LiteralPath $file -PathType Leaf)) {
        throw ('Installer/updater component is missing: '+$file)
    }
}
foreach($file in @($installer,$updater)) {
    $tokens=$null
    $errorsFound=$null
    $null=[System.Management.Automation.Language.Parser]::ParseFile(
        $file,[ref]$tokens,[ref]$errorsFound)
    if($errorsFound.Count -gt 0) {
        foreach($item in $errorsFound){Write-Error $item.Message}
        throw ('PowerShell script failed to parse: '+$file)
    }
}
$installSource=Get-Content -LiteralPath $installer -Raw -Encoding UTF8
$updateSource=Get-Content -LiteralPath $updater -Raw -Encoding UTF8
$source=$installSource+[Environment]::NewLine+$updateSource
$launcher=Get-Content -LiteralPath $installerBat -Raw -Encoding UTF8
$updateLauncher=Get-Content -LiteralPath $updaterBat -Raw -Encoding UTF8

$checks=@(
    @{label='64-bit Program Files';pattern='GetFolderPath\(''ProgramFiles''\)'},
    @{label='Japanese installed folder';pattern='\$installName\s*=\s*''日本語拡大鏡'''},
    @{label='backup and recovery';pattern='\$oldRenamed\s*=\s*\$true'},
    @{label='manual administrator elevation';pattern='-Verb\s+RunAs'},
    @{label='verified rolling GitHub release';pattern='tryout-latest'},
    @{label='SHA256 validation before replacement';pattern='Get-FileHash'},
    @{label='zip version match';pattern='\$bundledVersion\s+-ne\s+\$newVersion'},
    @{label='refuse update while reader is running';pattern='Assert-ProgramsClosed'},
    @{label='offline model integrity';pattern='ppocrv6_rec\.onnx'},
    @{label='permanent user shortcut';pattern='日本語拡大鏡を更新\.lnk'}
)
foreach($check in $checks) {
    if(-not [regex]::IsMatch($source,$check.pattern,[Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        throw ('Installer/update policy check failed: '+$check.label)
    }
}
if($source.Contains('D:\') -or $launcher.Contains('D:\') -or $updateLauncher.Contains('D:\')) {
    throw 'Updater cannot use drive D'
}
if(-not $launcher.Contains('-File "%~dp0Install-Magnifier-CDrive.ps1"')) {
    throw 'Installer BAT cannot invoke its companion script'
}
if(-not $updateLauncher.Contains('copy /y "%~dp0Update-Magnifier.ps1"') -or
   -not $updateLauncher.Contains('-File "%TASK%\Update-Magnifier.ps1"') -or
   -not $updateLauncher.Contains('cd /d "%TEMP%"')) {
    throw 'Updater BAT must run from a detached temporary copy to avoid locking Program Files'
}
if(-not $installSource.Contains("Update-Magnifier.ps1")) {
    throw 'Initial installer must reuse the verified manual updater'
}
Write-Host 'PASS: Japanese installer and manual updater parse under Windows PowerShell 5.1.'
Write-Host 'PASS: C-drive install, SHA256 verification, old-version rollback and shortcuts are present.'
Write-Host 'Note: Actual UAC, running-process detection and file replacement need a Windows 11 manual trial.'
