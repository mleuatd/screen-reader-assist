using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ScreenReaderAssist;

internal static class OfflinePaddleOcrTests
{
    private const string First="あら、プレゼントが届いています。";
    private const string Second="有用なものだといいですね。";

    public static async Task<int> RunAsync(string modelDirectory)
    {
        // Model absence must never trigger any attempt to download a model.
        using(var absent=new OfflinePaddleOcr(Path.Combine(Path.GetTempPath(),"no-local-ocr-models-ever")))
        {
            if(absent.ModelsAvailable)throw new Exception("No-model test has unexpected files.");
            using var empty=new Bitmap(64,64);
            var noText=await absent.RecognizeAsync(empty);
            if(noText is not null)throw new Exception("A missing local model unexpectedly produced OCR text.");
            Console.WriteLine("PASS: モデル不足時は通信・取得をせずにローカルWindows OCRに戻せる。");
        }

        using var ocr=new OfflinePaddleOcr(modelDirectory);
        if(!ocr.ModelsAvailable)
            throw new FileNotFoundException("配布モデルが両方揃っていません。製品ZIPには同梱が必須です。");
        var preload=Stopwatch.StartNew();
        await ocr.WarmUpAsync();
        preload.Stop();

        var elapsed=new List<double>();
        for(int variant=0;variant<3;variant++)
        {
            using var image=MakeImage(variant);
            var watch=Stopwatch.StartNew();
            var text=await ocr.RecognizeAsync(image);
            watch.Stop();
            elapsed.Add(watch.Elapsed.TotalMilliseconds);
            if(Normalize(text)!=Normalize(First+Second))
                throw new Exception($"日本語の全29文字が一致しません（条件{variant+1}、認識文字数={text?.Length??0}）。");
            Console.WriteLine($"PASS: 日本語画像 条件{variant+1} の全文一致。OCR所要時間={watch.Elapsed.TotalMilliseconds:F1}ms");
        }

        using(var prod=new OcrService(modelDirectory){UseHighAccuracyLocal=true})
        using(var sample=MakeImage(0))
        {
            var serviceText=await prod.RecognizeWithRegionFallbackAsync(sample,new Point(sample.Width/2,sample.Height/2),true,true);
            if(Normalize(serviceText)!=Normalize(First+Second))
                throw new Exception("本体の範囲読み上げ処理から日本語モデルが使用できませんでした。");
            Console.WriteLine("PASS: 既存のC#範囲OCRサービスから高精度モデルを直接呼べました。");
        }
        Console.WriteLine($"BENCH: 日本語モデル準備={preload.Elapsed.TotalMilliseconds:F1}ms、最初の認識={elapsed[0]:F1}ms、次の2回={elapsed[1]:F1}/{elapsed[2]:F1}ms。");
        Console.WriteLine("注意: ここにはマウスジェスチャー、画面取得、SAPI音声の発声開始時間は含まれません。");
        return 0;
    }

    private static Bitmap MakeImage(int variant)
    {
        using var background=new Bitmap(950,250,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(background))
        {
            g.Clear(variant==1?Color.FromArgb(241,239,230):Color.White);
            using var font=new Font("Yu Gothic",36,FontStyle.Bold);
            var firstBrush=variant==1?Brushes.SaddleBrown:Brushes.Black;
            var secondBrush=variant==2?Brushes.DarkBlue:firstBrush;
            g.DrawString(First,font,firstBrush,new PointF(24,42));
            g.DrawString(Second,font,secondBrush,new PointF(24,132));
        }
        return (Bitmap)background.Clone();
    }

    private static string Normalize(string? text)
    {
        return string.Concat((text??"").Where(c=>!char.IsWhiteSpace(c)));
    }
}
