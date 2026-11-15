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
$gesture=[AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('Win11Magnifier.MagnifierGestureMath',$false) } |
    Where-Object { $null -ne $_ } | Select-Object -First 1
if(-not $gesture){throw 'Magnifier gesture math was not compiled'}
$method=$gesture.GetMethod('HorizontalSteps',[System.Reflection.BindingFlags]'Static,NonPublic')
if(-not $method){throw 'Horizontal drag thresholds cannot be tested'}
foreach($case in @(
    @{ pixels=23; expected=0 },
    @{ pixels=24; expected=1 },
    @{ pixels=72; expected=3 },
    @{ pixels=-48; expected=-2 },
    @{ pixels=-23; expected=0 }
)) {
    $actual=[int]$method.Invoke($null,@([int]$case.pixels))
    if($actual -ne $case.expected) {
        throw "Incorrect horizontal gesture at $($case.pixels) px: $actual (expected $($case.expected))"
    }
}
$second=$gesture.GetMethod('IsSecondClick',[System.Reflection.BindingFlags]'Static,NonPublic')
if(-not $second){throw 'Double-click accessibility tolerance test function is missing'}
$systemSize=[System.Drawing.Size]::new(4,4)
foreach($case in @(
    @{ms=420;dx=18;dy=-15;result=$true},
    @{ms=420;dx=19;dy=0;result=$false},
    @{ms=501;dx=0;dy=0;result=$false},
    @{ms=0;dx=0;dy=0;result=$true},
    @{ms=-1;dx=0;dy=0;result=$false}
)) {
    $got=[bool]$second.Invoke($null,@(
        [int]$case.ms,[int]$case.dx,[int]$case.dy,500,$systemSize))
    if($got -ne $case.result){
        throw ("Unexpected double-click behavior: time=$($case.ms), dx=$($case.dx), dy=$($case.dy)")
    }
}
Write-Host 'PASS: Japanese magnifier double-click accepts 18px hand drift but rejects late/unrelated clicks.'
$viewer=[AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('Win11Magnifier.DedicatedViewer',$false) } |
    Where-Object { $null -ne $_ } | Select-Object -First 1
if(-not $viewer.GetMethod('ForwardPreviewMouse',[System.Reflection.BindingFlags]'Instance,NonPublic')) {
    throw 'Magnifier mouse gestures are not connected to global hook'
}
$policy=$compiled.GetMethod('ShouldBeginAltSelection',[System.Reflection.BindingFlags]'Static,NonPublic')
if(-not $policy) {throw 'Global Alt selection compatibility policy missing'}
foreach($case in @(
    @{msg=0x0204;alt=$true;ctrl=$false;win=$false;left=$false;expect=$true},
    @{msg=0x0201;alt=$true;ctrl=$false;win=$false;left=$false;expect=$false},
    @{msg=0x0201;alt=$true;ctrl=$false;win=$false;left=$true;expect=$true},
    @{msg=0x0204;alt=$true;ctrl=$true;win=$false;left=$false;expect=$false},
    @{msg=0x0204;alt=$true;ctrl=$false;win=$true;left=$false;expect=$false},
    @{msg=0x0204;alt=$false;ctrl=$false;win=$false;left=$false;expect=$false},
    @{msg=0x0202;alt=$true;ctrl=$false;win=$false;left=$true;expect=$false}
)) {
    $actual=[bool]$policy.Invoke($null,@([int]$case.msg,[bool]$case.alt,[bool]$case.ctrl,[bool]$case.win,[bool]$case.left))
    if($actual -ne $case.expect) {throw 'Alt-mouse compatibility policy regression'}
}
Write-Host 'PASS: Alt+right works, Alt+left opt-in; Ctrl/Win/ordinary mouse ignored.'
Write-Host 'PASS: Japanese magnifier Alt+right selection and global preview gesture hook compile.'
# Compile is not a visual verification; enforce the code-level accessibility contract.
$viewerSource=[IO.File]::ReadAllText((Join-Path (Split-Path -Parent $mouse) 'DedicatedMagnifier.cs'))
$mouseSource=[IO.File]::ReadAllText($mouse)
if($viewerSource -notmatch 'StyleAccessibleButton\(button\)' -or
   $viewerSource -notmatch 'StyleAccessibleButton\(b\)' -or
   $viewerSource -notmatch 'Color.FromArgb\(255, 224, 47\)' -or
   $viewerSource -notmatch 'Font\("Meiryo", 21f, FontStyle.Bold\)' -or
   $viewerSource -notmatch 'altLeftEnabled = false' -or
   $viewerSource -notmatch 'globalOcr.AllowAltLeft = altLeftEnabled') {
    throw 'Accessible pressed/focus button styling or safe Alt-left initialization missing'
}
$startCount=([regex]::Matches($mouseSource,[regex]::Escape('refreshTimer.Start();'))).Count
if($startCount -ne 1) {throw 'Idle selection timer regression: unexpected active refresh count'}
Write-Host 'PASS: yellow press/focus, bold Japanese controls, Alt-left opt-in, and idle-timer restraint.'
Write-Host 'PASS: 24px horizontal steps, negative drag direction and edge thresholds verified.'
