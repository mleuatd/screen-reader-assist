using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ScreenReaderAssist;

internal static class TextRegionDetectorRegressionTests
{
    public static void Run()
    {
        // Compare every rectangle (including order) with the unchanged previous implementation.
        int checks=0;
        foreach(var format in new[]{
            PixelFormat.Format32bppArgb, PixelFormat.Format32bppRgb,
            PixelFormat.Format32bppPArgb, PixelFormat.Format24bppRgb
        })
        foreach(var (width,height) in new[]{(240,130),(480,240),(760,390)})
        {
            using var bmp=new Bitmap(width,height,format);
            using(var g=Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255,250,250,245));
                using var font=new Font("Yu Gothic",Math.Max(13,width/24),FontStyle.Bold);
                g.DrawString("日本語の文字認識１２３",font,Brushes.Black,new PointF(16,20));
                g.DrawString("漢字とひらがな、カタカナ。",font,Brushes.DarkBlue,new PointF(14,height/2));
                var random=new Random(width*1009+height);
                for(int n=0;n<20;n++)
                {
                    int x=random.Next(width),y=random.Next(height);
                    int w=Math.Min(random.Next(5,25),width-x);
                    int h=Math.Min(random.Next(2,15),height-y);
                    using var brush=new SolidBrush(Color.FromArgb(255,random.Next(256),random.Next(256),random.Next(256)));
                    g.FillRectangle(brush,new Rectangle(x,y,w,h));
                }
            }
            foreach(var near in new[]{
                new Point(width/3,height/3),new Point(width-2,height-2),
                new Point(1,1)
            })
            foreach(int limit in new[]{1,2,3})
            {
                var expected=TextRegionDetectorReference.Propose(bmp,near,limit);
                var actual=TextRegionDetector.Propose(bmp,near,limit);
                if(!expected.SequenceEqual(actual))
                    throw new Exception($"日本語画像の領域検出にデグレ: {format} {width}x{height} point={near} limit={limit} expected={string.Join(";",expected)} actual={string.Join(";",actual)}");
                checks++;
            }
        }
        Console.WriteLine($"PASS: 日本語画像の検出領域・順序を旧実装と比較：{checks}通りで一致。");

        // Timing is reported, never assumed. CI machine variation must not cause false failures.
        using var perf=new Bitmap(1200,600,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(perf))
        {
            g.Clear(Color.White);
            using var font=new Font("Yu Gothic",36,FontStyle.Bold);
            for(int y=20;y<550;y+=64)
                g.DrawString("日本語OCR性能の比較：漢字・ひらがな・数字１２３",font,Brushes.Black,new PointF(24,y));
        }
        var p=new Point(300,180);
        _=TextRegionDetector.Propose(perf,p); // warmup
        _=TextRegionDetectorReference.Propose(perf,p);
        double oldMs=MedianMilliseconds(()=>TextRegionDetectorReference.Propose(perf,p));
        double newMs=MedianMilliseconds(()=>TextRegionDetector.Propose(perf,p));
        Console.WriteLine($"BENCH 日本語画像領域検出 1200x600：旧GetPixel={oldMs:F1}ms、新LockBits={newMs:F1}ms、比率={oldMs/Math.Max(0.001,newMs):F2}倍（CIでの参考値、実機OCR全体の速度ではありません）。");
    }

    private static double MedianMilliseconds(Action action)
    {
        var times=new double[5];
        for(int i=0;i<times.Length;i++)
        {
            var watch=Stopwatch.StartNew();
            action();
            watch.Stop();
            times[i]=watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        return times[times.Length/2];
    }
}
