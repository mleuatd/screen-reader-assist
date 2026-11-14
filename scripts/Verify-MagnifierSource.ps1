#Requires -Version 5.1
# Run in 64-bit Windows PowerShell 5.1. Compile-only: do not open a GUI.
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root 'prototypes/Win11MagnifierBase'
$viewer=Get-ChildItem -LiteralPath $source -File -Recurse -Filter 'DedicatedMagnifier.cs' |
    Select-Object -First 1
if(-not $viewer) {throw 'Magnifier source does not exist'}
$mouse=Join-Path $viewer.DirectoryName 'GlobalOcrSelection.cs'
if(-not (Test-Path -LiteralPath $mouse)){throw 'Magnifier mouse selection source missing'}

$refs=@()
foreach($name in @('System.Windows.Forms','System.Drawing','System.Speech',
                   'UIAutomationClient','UIAutomationTypes','WindowsBase')) {
    Add-Type -AssemblyName $name -ErrorAction Stop
    $assembly=[AppDomain]::CurrentDomain.GetAssemblies() |
      Where-Object { $_.GetName().Name -eq $name } | Select-Object -First 1
    if(-not $assembly -or -not $assembly.Location){throw ('Missing assembly '+$name)}
    $refs += $assembly.Location
}
Add-Type -Path @($viewer.FullName,$mouse) -ReferencedAssemblies $refs -ErrorAction Stop
$compiled=[AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('Win11Magnifier.GlobalOcrSelection',$false) } |
    Where-Object { $null -ne $_ } | Select-Object -First 1
if(-not $compiled){throw 'Magnifier hook type was not compiled'}
Write-Host 'PASS: Original three-monitor magnifier and Alt-right green selection compile under Windows PowerShell 5.1.'
