#Requires -Version 5.1
<#
  Offline-only product policy gate.
  Scope: Windows production application and separate native magnifier prototype.
  Development/CI experiments are intentionally not packaged into the product.
  Source scanning is defense-in-depth, NOT proof of zero network packets.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dirs = @(
    (Join-Path $root 'src/ScreenReaderAssist'),
    (Join-Path $root 'prototypes/Win11MagnifierBase')
)
$checks = @(
    @{ Label='HTTP clients and web requests'; Pattern='\b(HttpClient|HttpWebRequest|WebRequest|WebClient|RestClient|HttpListener)\b' },
    @{ Label='Socket/transport clients'; Pattern='\b(TcpClient|UdpClient|Socket|ClientWebSocket|DnsClient|SmtpClient)\b' },
    @{ Label='Networking namespaces'; Pattern='\bSystem\.Net\.(Http|Sockets|WebSockets|Mail|NetworkInformation)\b' },
    @{ Label='Cloud and telemetry SDK'; Pattern='\b(Google\.Cloud|Azure\.AI|Grpc\.Net|OpenTelemetry\.Exporter|ApplicationInsights|SentrySdk)\b' },
    @{ Label='PowerShell network commands'; Pattern='\b(Invoke-WebRequest|Invoke-RestMethod|Start-BitsTransfer|New-PSSession|Enter-PSSession)\b' },
    @{ Label='Native WinHTTP/WinINet network APIs'; Pattern='\b(WinHttpOpen|WinHttpConnect|WinHttpSendRequest|InternetOpen|InternetConnect|HttpSendRequest|WSAStartup)\b' },
    @{ Label='CLI downloaders'; Pattern='\b(curl(?:\.exe)?|wget(?:\.exe)?|bitsadmin(?:\.exe)?)\b' },
    @{ Label='Remote URL literal'; Pattern='https?://' }
)
$violations = [System.Collections.Generic.List[string]]::new()
$scanned=0
foreach ($dir in $dirs) {
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) {
        throw ('Product source directory missing: ' + $dir)
    }
    $files = Get-ChildItem -LiteralPath $dir -Recurse -File |
        Where-Object { $_.Extension -in @('.cs','.csproj','.ps1','.cmd','.bat','.config') }
    foreach ($file in $files) {
        $scanned++
        $source=Get-Content -LiteralPath $file.FullName -Encoding UTF8 -Raw
        foreach($check in $checks) {
            if([regex]::IsMatch($source,$check.Pattern,[System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
                $relative=$file.FullName.Substring($root.Length+1)
                $violations.Add(('{0}: {1}' -f $relative,$check.Label))
            }
        }
    }
}
if($scanned -eq 0){throw 'No product source files were checked.'}
if($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    throw ('OFFLINE POLICY FAILED: ' + $violations.Count + ' prohibited pattern(s).')
}
Write-Host ("PASS: {0} product source files contain no prohibited networking APIs, remote URLs, or telemetry SDK references." -f $scanned)
Write-Host 'Note: Static checks do not replace process-network monitoring and an offline Windows 11 usability test.'
