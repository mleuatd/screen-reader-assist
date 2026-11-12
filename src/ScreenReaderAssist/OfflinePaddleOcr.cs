using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using RapidOCRSharpOnnx;
using RapidOCRSharpOnnx.Configurations;
using RapidOCRSharpOnnx.Providers;
using RapidOCRSharpOnnx.Utils;

namespace ScreenReaderAssist;

/// <summary>
/// Fully local Japanese OCR, using only model files distributed beside the EXE.
/// Does not download models, access network services, or write screenshots to disk.
/// Single-thread access protects the native inference engine against concurrent
/// hotkey/hover requests. The previous Windows OCR remains a safe fallback.
/// </summary>
public sealed class OfflinePaddleOcr : IDisposable
{
    public const string DetectorFile = "ppocrv6_det.onnx";
    public const string RecognizerFile = "ppocrv6_rec.onnx";

    private readonly string _modelDirectory;
    private readonly SemaphoreSlim _singleInference = new(1,1);
    private RapidOCRSharp? _engine;
    private bool _broken;
    private bool _disposed;
    private bool _missingReported;

    public OfflinePaddleOcr(string? modelDirectory=null)
    {
        _modelDirectory=modelDirectory ?? Path.Combine(AppContext.BaseDirectory,"models");
    }

    public bool ModelsAvailable =>
        File.Exists(Path.Combine(_modelDirectory,DetectorFile)) &&
        File.Exists(Path.Combine(_modelDirectory,RecognizerFile));

    public string AvailabilityText => ModelsAvailable
        ? "高精度の日本語OCRモデルを利用できます（完全オフライン）"
        : "高精度OCRモデルがありません。Windows標準OCRを使用します（通信・自動取得なし）";

    /// <summary>Prepare the model on a worker thread before the first selection.</summary>
    public async Task WarmUpAsync()
    {
        if(!ModelsAvailable || _broken || _disposed)return;
        await _singleInference.WaitAsync().ConfigureAwait(false);
        try
        {
            if(_engine is null && !_broken)
                await Task.Run(EnsureEngine).ConfigureAwait(false);
        }
        finally { _singleInference.Release(); }
    }

    /// <summary>Null means not available or failed; callers fall back to local Windows OCR.</summary>
    public async Task<string?> RecognizeAsync(Bitmap image)
    {
        if(_disposed || _broken)return null;
        if(!ModelsAvailable)
        {
            if(!_missingReported)
            {
                _missingReported=true;
                DiagnosticLog.Warn("ローカルOCR","同梱日本語モデルがないためWindows標準OCRへ切り替えます。オンライン取得はしません。");
            }
            return null;
        }

        await _singleInference.WaitAsync().ConfigureAwait(false);
        try
        {
            if(_disposed || _broken)return null;
            // The Bitmap is owned by the caller until this method completes.
            // All CPU-intensive conversion / inference happens on a worker thread.
            return await Task.Run(()=>
            {
                var clock=Stopwatch.StartNew();
                try
                {
                    EnsureEngine();
                    using var encoded=new MemoryStream();
                    image.Save(encoded,ImageFormat.Png);
                    using var pixels=Cv2.ImDecode(encoded.ToArray(),ImreadModes.Color);
                    if(pixels.Empty()) throw new InvalidOperationException("画面画像をOCR用に読み込めませんでした。");
                    var result=_engine!.RecognizeText(pixels);
                    var text=result.TextBlocks?.Trim() ?? "";
                    DiagnosticLog.Info("日本語OCR性能",
                        $"高精度ローカルモデル：画像={image.Width}x{image.Height}px、文字数={text.Length}、処理={clock.Elapsed.TotalMilliseconds:F1}ms。");
                    return text.Length==0?null:text;
                }
                catch(Exception ex)
                {
                    // Do not log screen contents or the recognized Japanese text.
                    _broken=true;
                    DiagnosticLog.Warn("ローカルOCR",
                        $"高精度モデルの認識に失敗（{ex.GetType().Name}）。この起動中はWindows標準OCRへ切り替えます。");
                    return null;
                }
            }).ConfigureAwait(false);
        }
        finally { _singleInference.Release(); }
    }

    private void EnsureEngine()
    {
        if(_engine is not null || _broken)return;
        try
        {
            var det=Path.Combine(_modelDirectory,DetectorFile);
            var rec=Path.Combine(_modelDirectory,RecognizerFile);
            if(!File.Exists(det) || !File.Exists(rec))
                throw new FileNotFoundException("同梱の日本語OCRモデルがありません。");
            var watch=Stopwatch.StartNew();
            // Explicit local file paths: no SDK automatic model discovery/download.
            var config=new OcrConfig(det,rec,LangRec.CH,OCRVersion.PPOCRV6);
            _engine=new RapidOCRSharp(new ExecutionProviderCPU(config));
            DiagnosticLog.Info("ローカルOCR",$"オフライン日本語モデルを準備しました。初期化={watch.Elapsed.TotalMilliseconds:F1}ms。");
        }
        catch(Exception ex)
        {
            _broken=true;
            DiagnosticLog.Warn("ローカルOCR",
                $"モデル初期化ができません（{ex.GetType().Name}）。Windows OCRへ切り替えます。通信はしません。");
        }
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;
        // Dispose occurs at application shutdown; background tasks already holding the
        // semaphore can finish without use-after-dispose of native inference memory.
        _singleInference.Wait();
        try { _engine?.Dispose();_engine=null; }
        finally { _singleInference.Release();_singleInference.Dispose(); }
    }
}
