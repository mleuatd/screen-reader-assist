#Requires -Version 7.0
<#
  Build/CI-side provisioning only. This script is NEVER included in the
  ScreenReaderAssist user ZIP and NEVER executed by the user application.
  All models and native dependencies are bundled before distribution.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Destination)

$ErrorActionPreference='Stop'
$destinationPath=[System.IO.Path]::GetFullPath($Destination)
$models=Join-Path $destinationPath 'models'
New-Item -ItemType Directory -Force -Path $models | Out-Null
$tmp=Join-Path ([System.IO.Path]::GetTempPath()) ('OfflinePaddleOCR-Build-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

try {
    $base='https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0'
    foreach($kind in @('det','rec')) {
        $name="PP-OCRv6_small_${kind}_onnx_infer"
        $tar=Join-Path $tmp ($name+'.tar')
        Write-Host ("CI-only downloading official model archive: "+$name)
        Invoke-WebRequest -Uri ($base+'/'+$name+'.tar') -OutFile $tar -TimeoutSec 360
        if((Get-Item $tar).Length -lt 100000){throw "Empty or invalid official archive: $name"}
        & tar -xf $tar -C $tmp
        if($LASTEXITCODE -ne 0){throw "Failed to unpack official ONNX model: $name"}
        $onnx=Get-ChildItem $tmp -Recurse -Filter '*.onnx' -File |
            Where-Object { $_.FullName -like ('*'+$name+'*') } |
            Select-Object -First 1
        if(-not $onnx){throw "Official ONNX model missing: $name"}
        # Pin the exact model graph successfully tested in Japanese Windows CI.
        # Reject silently replaced model artifacts, even from an official URL.
        $expected=if($kind -eq 'det') {
            'D73E0058B7A8086BBD57F3D10B8BCD4FF95363F67E06E2762B5E814FE9C9410E'
        } else {
            '5435FD747C9E0EFE15A96D0B378D5BD157E9492ED8FD80EDF08F30D02FA24634'
        }
        $digest=(Get-FileHash -LiteralPath $onnx.FullName -Algorithm SHA256).Hash
        if($digest -ne $expected) {throw "Official model SHA256 does not match tested model: $name"}
        $nameOut=if($kind -eq 'det'){'ppocrv6_det.onnx'}else{'ppocrv6_rec.onnx'}
        Copy-Item -LiteralPath $onnx.FullName -Destination (Join-Path $models $nameOut) -Force
        Write-Host ("Bundled local model: $nameOut / $($onnx.Length) bytes")
    }

    # License files are for distribution. No URL is embedded in app executable.
    $licenses=Join-Path $destinationPath 'licenses'
    New-Item -ItemType Directory -Force -Path $licenses | Out-Null
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/main/LICENSE' -OutFile (Join-Path $licenses 'PaddleOCR-Apache-2.0.txt') -TimeoutSec 90
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/meloht/RapidOCRSharpOnnx/master/LICENSE.txt' -OutFile (Join-Path $licenses 'RapidOCRSharpOnnx-Apache-2.0.txt') -TimeoutSec 90
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/shimat/opencvsharp/master/LICENSE' -OutFile (Join-Path $licenses 'OpenCvSharp-Apache-2.0.txt') -TimeoutSec 90
    Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/microsoft/onnxruntime/main/LICENSE' -OutFile (Join-Path $licenses 'OnnxRuntime-MIT.txt') -TimeoutSec 90
    foreach($license in (Get-ChildItem $licenses -File)){
        if($license.Length -lt 1000){throw "Missing third-party license: $($license.Name)"}
    }
    $lines=@(
        '# Offline Japanese OCR provenance',
        'The models are bundled at build time. The Windows application never downloads them.',
        'PP-OCRv6_small_det_onnx_infer and PP-OCRv6_small_rec_onnx_infer',
        'Origin: PaddlePaddle official model distribution (paddle-model-ecology.bj.bcebos.com)',
        'Inference: RapidOCRSharpOnnx 1.4.0, OpenCvSharp5, ONNX Runtime CPU.',
        'Source and third-party license files are in the licenses folder.'
    )
    $lines | Set-Content -LiteralPath (Join-Path $models 'README.txt') -Encoding utf8
    foreach($m in (Get-ChildItem $models -Filter '*.onnx' -File)){
        $sha=(Get-FileHash $m.FullName -Algorithm SHA256).Hash
        Write-Host ("MODEL SHA256: $($m.Name) $sha")
        Add-Content -LiteralPath (Join-Path $models 'README.txt') -Value ("SHA256 $($m.Name) $sha")
    }
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
