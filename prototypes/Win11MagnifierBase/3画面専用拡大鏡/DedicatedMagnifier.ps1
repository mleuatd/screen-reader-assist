# Windows 11 three-monitor dedicated magnifier launcher.
# Windows PowerShell 5.1 (64-bit), STA.
$ErrorActionPreference = 'Stop'
try {
    if (-not [Environment]::Is64BitProcess) {
        throw 'この機能は64ビットのWindows PowerShellで起動してください。'
    }
    $path = Join-Path $PSScriptRoot 'DedicatedMagnifier.cs'
    $ocrPath = Join-Path $PSScriptRoot 'GlobalOcrSelection.cs'
    if (-not (Test-Path -LiteralPath $path)) {
        throw '専用拡大鏡のソースファイルが見つかりません。'
    }
    Set-Location -LiteralPath $PSScriptRoot
    Add-Type -Path @($path, $ocrPath) -ReferencedAssemblies @('System.Windows.Forms.dll','System.Drawing.dll','System.Speech.dll','UIAutomationClient.dll','UIAutomationTypes.dll','WindowsBase.dll') -ErrorAction Stop
    [Win11Magnifier.DedicatedMagnifierEntry]::Start()
}
catch {
    Write-Host ('起動エラー: ' + $_.Exception.Message) -ForegroundColor Red
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
        [void][System.Windows.Forms.MessageBox]::Show(
            $_.Exception.Message,
            '3画面専用拡大鏡 - 起動失敗',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        )
    } catch { }
    exit 1
}
