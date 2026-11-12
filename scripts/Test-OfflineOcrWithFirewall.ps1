#Requires -Version 7.0
<#
  GitHub-hosted Windows only: run Japanese OCR with outbound traffic blocked
  for the entire test process, including its native DLLs. Always remove rule.
  This proves offline usability, not the absence of connection attempts.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Models)

$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$tests=Join-Path $root 'tests/ScreenReaderAssist.Tests/bin/Release'
$binary=Get-ChildItem -LiteralPath $tests -Filter 'ScreenReaderAssist.Tests.exe' -Recurse -File |
    Where-Object { $_.FullName -notlike '*\ref\*' } |
    Select-Object -First 1
if(-not $binary){throw 'The Windows test executable does not exist. Build it first.'}

$modelDir=[IO.Path]::GetFullPath($Models)
foreach($name in @('ppocrv6_det.onnx','ppocrv6_rec.onnx')) {
    if(-not(Test-Path (Join-Path $modelDir $name))) {
        throw ('Required offline model is missing: '+$name)
    }
}

$rule='ScreenReaderAssist-CI-offline-japanese-'+[guid]::NewGuid().ToString('N')
try {
    New-NetFirewallRule -DisplayName $rule -Direction Outbound -Action Block -Program $binary.FullName -Profile Any -Enabled True | Out-Null
    Write-Host ('Network access blocked for OCR test executable: '+$binary.Name)
    & $binary.FullName '--paddle-ja' $modelDir
    $code=$LASTEXITCODE
    if($code -ne 0){throw ('Offline Japanese OCR test returned '+$code)}
    Write-Host 'PASS: Bundled Japanese PP-OCRv6 worked with outbound Windows Firewall blocking the test process.'
}
finally {
    Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue
}
