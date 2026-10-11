using System.Drawing;
using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Globalization;
using Windows.Storage.Streams;
namespace ScreenReaderAssist;

public sealed class OcrService
{
    public async Task<string> RecognizeAsync(Bitmap bmp, bool preferJapanese)
    {
        var result=await RecognizeResultAsync(bmp,preferJapanese);
        return result.Text?.Trim() ?? "";
    }
    /// <summary>Read the OCR line nearest the cursor, not every nearby link or timestamp.</summary>
    public async Task<string> RecognizeNearPointAsync(Bitmap bmp, Point near, bool preferJapanese)
    {
        var result=await RecognizeResultAsync(bmp,preferJapanese);
        var closest=result.Lines.Select(line => {
            var boxes=line.Words.Select(w=>w.BoundingRect).ToArray();
            if (boxes.Length==0) return (text:"",distance:double.PositiveInfinity);
            double left=boxes.Min(b=>b.X), top=boxes.Min(b=>b.Y);
            double right=boxes.Max(b=>b.X+b.Width),bottom=boxes.Max(b=>b.Y+b.Height);
            var dx=Math.Max(left-near.X,Math.Max(0,near.X-right));
            var dy=Math.Max(top-near.Y,Math.Max(0,near.Y-bottom));
            return (text:line.Text?.Trim()??"",distance:dx*dx+dy*dy);
        }).OrderBy(x=>x.distance).FirstOrDefault();
        return closest.distance<=80*80 ? closest.text : "";
    }
    private static async Task<OcrResult> RecognizeResultAsync(Bitmap bmp, bool preferJapanese)
    {
        OcrEngine? engine = null;
        if (preferJapanese && OcrEngine.IsLanguageSupported(new Language("ja-JP")))
            engine = OcrEngine.TryCreateFromLanguage(new Language("ja-JP"));
        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null) throw new InvalidOperationException("利用できるWindows OCR言語がありません。Windowsの日本語言語機能を確認してください。");
        // Windows OCR imposes a maximum bitmap dimension; scale large captures down.
        Bitmap input=bmp;
        Bitmap? resized=null;
        var max=OcrEngine.MaxImageDimension;
        if(Math.Max(bmp.Width,bmp.Height)>max) {
            double scale=(double)max/Math.Max(bmp.Width,bmp.Height);
            resized=new Bitmap(bmp,new Size(Math.Max(1,(int)(bmp.Width*scale)),Math.Max(1,(int)(bmp.Height*scale))));
            input=resized;
        }
        using var ms=new MemoryStream();
        try { input.Save(ms,ImageFormat.Png); }
        finally { resized?.Dispose(); }
        using var stream=new InMemoryRandomAccessStream();
        using (var writer=new DataWriter(stream)) {
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var decoder=await BitmapDecoder.CreateAsync(stream);
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result=await engine.RecognizeAsync(bitmap);
        return result;
    }
}
