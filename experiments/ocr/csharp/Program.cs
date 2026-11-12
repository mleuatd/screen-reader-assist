using System.Diagnostics;
using RapidOCRSharpOnnx;
using RapidOCRSharpOnnx.Configurations;
using RapidOCRSharpOnnx.Providers;
using RapidOCRSharpOnnx.Utils;

// Standalone C# probe: never changes the shipping ScreenReaderAssist OCR provider.
// Supply ONNX model paths from official PaddleOCR downloads. No screen uploads.
Console.OutputEncoding = System.Text.Encoding.UTF8;
if(args.Length!=3)
{
    Console.WriteLine("実行方法: RapidOcrCSharpProbe <検出モデル.onnx> <認識モデル.onnx> <日本語画像.png>");
    Console.WriteLine("この実験用ソフトはモデルが指定されない限りOCRを起動しません。");
    return;
}
foreach(var file in args)
{
    if(!File.Exists(file)) throw new FileNotFoundException("必要なモデルまたは画像ファイルがありません。",file);
}
var init=Stopwatch.StartNew();
var config=new OcrConfig(args[0],args[1],LangRec.CH,OCRVersion.PPOCRV6);
using var engine=new RapidOCRSharp(new ExecutionProviderCPU(config));
init.Stop();
Console.WriteLine($"日本語C# PP-OCRv6 初期化: {init.Elapsed.TotalMilliseconds:F1} ms");
for(var i=0;i<4;i++)
{
    var elapsed=Stopwatch.StartNew();
    var result=engine.RecognizeText(args[2]);
    elapsed.Stop();
    Console.WriteLine($"日本語C# PP-OCRv6 認識{i+1}: {elapsed.Elapsed.TotalMilliseconds:F1} ms");
    if(i==0) Console.WriteLine("OCR出力: "+result.ToString());
}
