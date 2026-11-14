#Requires -Version 5.1
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$ps1=Join-Path $root 'installer/Install-Magnifier-CDrive.ps1'
$bat=Join-Path $root 'installer/日本語拡大鏡_インストール.bat'
$source=Get-Content -LiteralPath $ps1 -Raw -Encoding UTF8
$launcher=Get-Content -LiteralPath $bat -Raw -Encoding UTF8
$tokens=$null
$parseErrors=$null
$null=[System.Management.Automation.Language.Parser]::ParseFile($ps1,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count -gt 0) {
    foreach($parseError in $parseErrors){Write-Error $parseError.Message}
    throw 'Installer PowerShell parse failed'
}
$checks=@(
    @{label='x64 Program Files';pattern='GetFolderPath\(\x27ProgramFiles\x27\)'},
    @{label='Japanese installed folder';pattern='\$installName\s*=\s*\x27日本語拡大鏡\x27'},
    @{label='existing install rollback';pattern='\$oldRenamed\s*=\s*\$true'},
    @{label='UAC promotion';pattern='-Verb\s+RunAs'},
    @{label='single official GitHub download';pattern='https://github\.com/mleuatd/screen-reader-assist/releases/download/'},
    @{label='Japanese app launched by BAT';pattern='拡大鏡実行\.bat'},
    @{label='offline model verification';pattern='ppocrv6_rec\.onnx'}
)
foreach($check in $checks){
    if(-not [regex]::IsMatch($source,$check.pattern,[Text.RegularExpressions.RegexOptions]::IgnoreCase)){
        throw ('Installer missing check: '+$check.label)
    }
}
if($source.Contains('D:\') -or $launcher.Contains('D:\')){throw 'The installer must not use drive D'}
if(-not $launcher.Contains('-File "%~dp0Install-Magnifier-CDrive.ps1"')){
    throw 'Installer BAT cannot invoke the PowerShell companion script'
}
Write-Host 'PASS: C-drive 64-bit Japanese installer script parses, requires admin and validates offline models'
Write-Host 'Runtime download and actual UAC/Program Files write need a Windows 11 user trial'
