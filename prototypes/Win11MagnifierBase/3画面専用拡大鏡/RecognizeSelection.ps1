# Windows PowerShell 5.1 only. Offline Japanese OCR using Windows.Media.Ocr.
# Receives a temporary screen-capture PNG and writes UTF-8 OCR text.
# No cloud connection, clipboard access, or third-party engine required.
param(
    [Parameter(Mandatory=$true)][string]$ImagePath,
    [Parameter(Mandatory=$true)][string]$TextPath,
    [Parameter(Mandatory=$true)][string]$ErrorPath
)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
    $null = [Windows.Foundation.IAsyncOperation`1, Windows.Foundation, ContentType = WindowsRuntime]
    $null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Foundation, ContentType = WindowsRuntime]
    $null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType = WindowsRuntime]
    $null = [Windows.Storage.Streams.RandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]

    # Japanese OCR language resources are required, not merely a Japanese UI.
    $installed = @([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages)
    $japanese = @($installed | Where-Object { $_.LanguageTag -match '^ja($|-)' })
    if ($japanese.Count -eq 0) {
        throw '日本語OCRがありません。Windowsの「設定 → 時刻と言語 → 言語と地域」で日本語の言語機能（OCR）を追加してください。'
    }
    $ocr = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage('ja-JP')
    if ($null -eq $ocr) { throw '日本語OCRエンジンを初期化できません。' }

    $awaiter = [WindowsRuntimeSystemExtensions].GetMember('GetAwaiter') |
        Where-Object { $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } |
        Select-Object -First 1
    if ($null -eq $awaiter) { throw 'Windows Runtime OCRの非同期処理を初期化できません。' }
    function AwaitWinRT($AsyncOperation, $ResultType) {
        $method = $awaiter.MakeGenericMethod($ResultType)
        $method.Invoke($null, @($AsyncOperation)).GetResult()
    }
    $stream = [IO.File]::OpenRead($ImagePath)
    $random = $null
    $bitmap = $null
    try {
        $random = [IO.WindowsRuntimeStreamExtensions]::AsRandomAccessStream($stream)
        $decoder = AwaitWinRT ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($random)) ([Windows.Graphics.Imaging.BitmapDecoder])
        $bitmap = AwaitWinRT ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
        $result = AwaitWinRT ($ocr.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        $text = (($result.Lines | ForEach-Object { $_.Text }) -join "`n").Trim()
        [IO.File]::WriteAllText($TextPath, $text, [Text.UTF8Encoding]::new($false))
    }
    finally {
        if ($null -ne $bitmap) { $bitmap.Dispose() }
        if ($null -ne $random) { $random.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
    }
    exit 0
}
catch {
    try { [IO.File]::WriteAllText($ErrorPath, $_.Exception.Message, [Text.UTF8Encoding]::new($false)) } catch {}
    exit 1
}
