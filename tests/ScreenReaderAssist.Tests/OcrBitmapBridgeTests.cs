using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ScreenReaderAssist;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

internal static class OcrBitmapBridgeTests
{
    public static async Task RunAsync()
    {
        var cases=0;
        foreach(var format in new[]{PixelFormat.Format32bppArgb,PixelFormat.Format32bppRgb,PixelFormat.Format24bppRgb})
        {
            using var picture=MakeJapaneseImage(530,260,format);
            await CompareAsync(picture,format.ToString());
            cases++;
        }
        // Transparent bitmaps must use the legacy decoder, not lose alpha.
        using(var transparent=new Bitmap(220,120,PixelFormat.Format32bppArgb))
        {
            using(var g=Graphics.FromImage(transparent))
            {
                g.Clear(Color.Transparent);
                g.DrawString("日本語の文章",SystemFonts.DefaultFont,Brushes.Black,20,30);
            }
            await CompareAsync(transparent,"transparency fallback");
            cases++;
        }
        Console.WriteLine($"PASS: 日本語画像のOCR入力変換：旧PNG経路と新高速経路の全ピクセル一致 {cases}件。");

        using var perf=MakeJapaneseImage(1200,600,PixelFormat.Format32bppArgb);
        // Warm up JIT/Windows graphics services. Measure image preparation only.
        using(var old=await OcrBitmapBridge.CreateLegacyPngAsync(perf)) {}
        using(var fresh=await OcrBitmapBridge.CreateAsync(perf)) {}
        var oldTimes=new double[5];var newTimes=new double[5];
        for(int i=0;i<5;i++)
        {
            // Alternating order reduces systematic warmup bias.
            if(i%2==0)
            {
                oldTimes[i]=await MeasureAsync(()=>OcrBitmapBridge.CreateLegacyPngAsync(perf));
                newTimes[i]=await MeasureAsync(()=>OcrBitmapBridge.CreateAsync(perf));
            }
            else
            {
                newTimes[i]=await MeasureAsync(()=>OcrBitmapBridge.CreateAsync(perf));
                oldTimes[i]=await MeasureAsync(()=>OcrBitmapBridge.CreateLegacyPngAsync(perf));
            }
        }
        Array.Sort(oldTimes);Array.Sort(newTimes);
        Console.WriteLine($"BENCH 日本語画像 1200x600 → Windows OCR用Bitmap変換：旧PNG中央値={oldTimes[2]:F1}ms、新直接変換中央値={newTimes[2]:F1}ms、速度比={oldTimes[2]/Math.Max(0.001,newTimes[2]):F2}倍。認識処理と音声開始の時間は含みません。");
    }

    private static async Task<double> MeasureAsync(Func<Task<SoftwareBitmap>> convert)
    {
        var clock=Stopwatch.StartNew();
        using(var converted=await convert()) {}
        clock.Stop();return clock.Elapsed.TotalMilliseconds;
    }

    private static Bitmap MakeJapaneseImage(int width,int height,PixelFormat format)
    {
        var bmp=new Bitmap(width,height,format);
        using(var g=Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            using var font=new Font("Yu Gothic",Math.Max(15,width/35),FontStyle.Bold);
            g.DrawString("あら、プレゼントが届いています。",font,Brushes.SaddleBrown,new PointF(16,22));
            g.DrawString("有用なものだといいですね。",font,Brushes.DarkBlue,new PointF(16,height/2));
            using var randomBrush=new SolidBrush(Color.FromArgb(255,75,140,200));
            g.FillRectangle(randomBrush,new Rectangle(width/3,height*3/4,width/2,height/12));
        }
        return bmp;
    }

    private static async Task CompareAsync(Bitmap image,string title)
    {
        using var legacy=await OcrBitmapBridge.CreateLegacyPngAsync(image);
        using var optimized=await OcrBitmapBridge.CreateAsync(image);
        if(legacy.PixelWidth!=optimized.PixelWidth || legacy.PixelHeight!=optimized.PixelHeight ||
           legacy.BitmapPixelFormat!=optimized.BitmapPixelFormat)
            throw new Exception("日本語画像の変換デグレ（サイズかピクセル形式）: "+title);
        var oldBytes=CopyPixels(legacy);
        var newBytes=CopyPixels(optimized);
        if(!oldBytes.SequenceEqual(newBytes))
        {
            int mismatch=-1;
            for(int i=0;i<oldBytes.Length;i++)if(oldBytes[i]!=newBytes[i]){mismatch=i;break;}
            throw new Exception($"日本語画像の画素デグレ: {title}; index={mismatch}, old={oldBytes[mismatch]}, new={newBytes[mismatch]}");
        }
    }

    private static byte[] CopyPixels(SoftwareBitmap bitmap)
    {
        int count=checked(bitmap.PixelWidth*bitmap.PixelHeight*4);
        var buffer=new Windows.Storage.Streams.Buffer((uint)count);
        buffer.Length=(uint)count;
        bitmap.CopyToBuffer(buffer);
        using var reader=DataReader.FromBuffer(buffer);
        var bytes=new byte[count];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
