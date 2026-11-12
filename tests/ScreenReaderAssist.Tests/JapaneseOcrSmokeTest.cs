using System.Drawing;
using System.Text;
using Windows.Globalization;
using Windows.Media.Ocr;
using ScreenReaderAssist;

// 実際の日本語OCRが利用可能なWindows環境だけで行う統合スモークテスト。
// 英語のOCRによる代替はしない。日本語OCRが無ければ終了コード2を返す。
internal static class JapaneseOcrSmokeTest
{
    public static async Task<int> RunAsync()
    {
        Console.OutputEncoding=Encoding.UTF8;
        try
        {
            if(!OcrEngine.IsLanguageSupported(new Language("ja-JP")))
            {
                Console.WriteLine("日本語OCR：未検証（このWindowsランナーには日本語OCR言語機能がありません）。英語OCRで代用しません。");
                return 2;
            }
            const string sample="日本語の文字認識を確認します。";
            using var bmp=new Bitmap(1200,190);
            using(var g=Graphics.FromImage(bmp))
            using(var font=new Font("Yu Gothic",50,FontStyle.Regular,GraphicsUnit.Pixel))
            {
                g.Clear(Color.White);
                g.DrawString(sample,font,Brushes.Black,new PointF(30,40));
            }
            var service=new OcrService();
            var recognized=await service.RecognizeAsync(bmp,true);
            // 複数の日本語漢字を認識したことを確認。結果の内容はログに残さない。
            var jpCount=recognized.Count(c=>(c>=0x3040 && c<=0x30ff)||(c>=0x3400 && c<=0x9fff));
            if(jpCount<4)
            {
                Console.Error.WriteLine($"日本語OCR：失敗（日本語文字数={jpCount}）。");
                return 3;
            }
            Console.WriteLine($"日本語OCR：成功（日本語文字数={jpCount}）。画像や認識文章の本文は記録しません。");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine($"日本語OCR：失敗（{ex.GetType().Name}）。");
            return 3;
        }
    }
}
