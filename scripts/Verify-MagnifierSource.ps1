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
# Edge drawer geometry tests do not need real screens or mouse injection.
$drawerType=[AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('Win11Magnifier.DrawerMath',$false) } |
    Where-Object { $null -ne $_ } | Select-Object -First 1
if(-not $drawerType) {throw 'Missing edge drawer geometry policy'}
$flags=[System.Reflection.BindingFlags]'Static,NonPublic'
$reveal=$drawerType.GetMethod('RevealSide',$flags)
$target=$drawerType.GetMethod('Target',$flags)
$approach=$drawerType.GetMethod('Approach',$flags)
if(-not $reveal -or -not $target -or -not $approach) {throw 'Incomplete edge drawer geometry API'}
$client=[System.Drawing.Rectangle]::new(-1920,0,1920,1080)
$leftScreen=$client
$desktop=[System.Drawing.Rectangle]::new(-1920,0,5760,1080)
$rightClient=[System.Drawing.Rectangle]::new(1920,0,1920,1080)
$rightScreen=$rightClient
$centerScreen=[System.Drawing.Rectangle]::new(0,0,1920,1080)
$checks=@(
    @{p=[System.Drawing.Point]::new(-1918,550);c=$client;s=$leftScreen;held=$false;want='Left'},
    @{p=[System.Drawing.Point]::new(-1000,4);c=$client;s=$leftScreen;held=$false;want='Top'},
    @{p=[System.Drawing.Point]::new(3835,550);c=$rightClient;s=$rightScreen;held=$false;want='Right'},
    @{p=[System.Drawing.Point]::new(3000,7);c=$rightClient;s=$rightScreen;held=$false;want='Top'},
    @{p=[System.Drawing.Point]::new(-1919,550);c=$client;s=$leftScreen;held=$true;want='None'},
    @{p=[System.Drawing.Point]::new(-900,500);c=$client;s=$leftScreen;held=$false;want='None'},
    @{p=[System.Drawing.Point]::new(-1921,500);c=$client;s=$leftScreen;held=$false;want='None'},
    @{p=[System.Drawing.Point]::new(1923,500);c=$rightClient;s=$rightScreen;held=$false;want='None'},
    @{p=[System.Drawing.Point]::new(2,500);c=$centerScreen;s=$centerScreen;held=$false;want='None'},
    @{p=[System.Drawing.Point]::new(1917,500);c=$centerScreen;s=$centerScreen;held=$false;want='None'},
    @{p=[System.Drawing.Point]::new(1000,2);c=$centerScreen;s=$centerScreen;held=$false;want='Top'}
)
foreach($case in $checks) {
    $side=$reveal.Invoke($null,@($case.p,$case.c,$case.s,$desktop,[bool]$case.held))
    if($side.ToString() -ne $case.want) {
        throw ('Edge drawer mismatch: got '+$side+' expected '+$case.want)
    }
}
$sideType=[AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('Win11Magnifier.DrawerSide',$false) } |
    Where-Object { $null -ne $_ } | Select-Object -First 1
$size=[System.Drawing.Size]::new(1920,1080)
$panel=[System.Drawing.Size]::new(1020,249)
foreach($check in @(
    @{name='Left';open=$false;x=-1020},
    @{name='Left';open=$true;x=0},
    @{name='Right';open=$false;x=1920},
    @{name='Right';open=$true;x=900}
)) {
    $side=[Enum]::Parse($sideType,$check.name)
    $point=$target.Invoke($null,@($size,$panel,$side,540,[bool]$check.open))
    if($point.X -ne $check.x) {throw 'Incorrect off-screen drawer slide position'}
}
$top=[Enum]::Parse($sideType,'Top')
$hidden=$target.Invoke($null,@($size,$panel,$top,950,$false))
$shown=$target.Invoke($null,@($size,$panel,$top,950,$true))
if($hidden.Y -ne -249 -or $shown.Y -ne 0) {throw 'Top drawer did not slide vertically'}
if([int]$approach.Invoke($null,@(-200,0)) -le -200) {throw 'Drawer animation did not advance'}
if([int]$approach.Invoke($null,@(0,0)) -ne 0) {throw 'Drawer animation never settled'}
$close=$drawerType.GetMethod('ShouldClose',$flags)
if(-not $close) {throw 'Drawer auto-hide policy missing'}
foreach($case in @(
    @{now=1499;last=1000;held=$false;menu=$false;expected=$false},
    @{now=1500;last=1000;held=$false;menu=$false;expected=$true},
    @{now=2500;last=1000;held=$true;menu=$false;expected=$false},
    @{now=2500;last=1000;held=$false;menu=$true;expected=$false}
)) {
    $result=[bool]$close.Invoke($null,@([int]$case.now,[int]$case.last,[bool]$case.held,[bool]$case.menu))
    if($result -ne $case.expected){throw 'Drawer hide grace or interaction suppression regression'}
}
$retain=$drawerType.GetMethod('RetainForInteraction',$flags)
if(-not $retain) {throw 'Drawer interaction retention policy missing'}
foreach($case in @(
    @{held=$false;menu=$false;text=$false;keyboard=$false;focused=$true;large=$false;want=$false},
    @{held=$true;menu=$false;text=$false;keyboard=$false;focused=$false;large=$false;want=$true},
    @{held=$false;menu=$true;text=$false;keyboard=$false;focused=$false;large=$false;want=$true},
    @{held=$false;menu=$false;text=$true;keyboard=$false;focused=$false;large=$false;want=$true},
    @{held=$false;menu=$false;text=$false;keyboard=$true;focused=$true;large=$false;want=$true},
    @{held=$false;menu=$false;text=$false;keyboard=$true;focused=$false;large=$false;want=$false},
    @{held=$false;menu=$false;text=$false;keyboard=$false;focused=$false;large=$true;want=$true}
)) {
    $got=[bool]$retain.Invoke($null,@(
        [bool]$case.held,[bool]$case.menu,[bool]$case.text,
        [bool]$case.keyboard,[bool]$case.focused,[bool]$case.large))
    if($got -ne $case.want) {throw 'Drawer stuck-open or active-input hiding regression'}
}
Write-Host 'PASS: ordinary mouse button focus does not stick drawer; keyboard, text, menus retain it.'
# The 500ms countdown must reset while a form control remains focused.
# Otherwise pressing Tab or editing the speech field hides the UI underneath users.
if($viewerSource -notmatch 'controlDrawer.ContainsFocus' -or
   $viewerSource -notmatch 'lastDrawerInteraction = now;' -or
   $viewerSource -notmatch 'displaySelector.DroppedDown') {
    throw 'Drawer must stay shown while controls are focused or a dropdown is open'
}
Write-Host 'PASS: drawer stays visible during keyboard focus and text entry.'
Write-Host 'PASS: drawer closes after 500 ms; active drag and open menus prevent accidental hiding.'
# Mouse pointer near a desktop seam must not trigger the side drawer.
$wideClient=[System.Drawing.Rectangle]::new(0,0,1920,1080)
$seamPointer=[System.Drawing.Point]::new(1918,480)
$seam=$reveal.Invoke($null,@($seamPointer,$wideClient,$centerScreen,$desktop,$false))
if($seam.ToString() -ne 'None') {throw 'Interior display seam activated side drawer'}
# No C# WinForms GUI launch is performed by this test job.
Write-Host 'PASS: left/right/top reveal, negative monitor coordinates, drag suppression, hide and slide math.'
Write-Host 'PASS: 24px horizontal steps, negative drag direction and edge thresholds verified.'
