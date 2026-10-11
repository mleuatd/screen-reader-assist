using ScreenReaderAssist;
using System.Drawing;
using System.Text.Json;

if(args.Contains("--benchmark-tts",StringComparer.OrdinalIgnoreCase)) {NativeTtsBenchmark.Run();return;}
if(args.Contains("--benchmark-tts-ja",StringComparer.OrdinalIgnoreCase)) {Environment.ExitCode=JapaneseTtsBenchmark.Run();return;}
if(args.Contains("--benchmark-onecore-ja",StringComparer.OrdinalIgnoreCase)) {Environment.ExitCode=await OneCoreJapaneseBenchmark.RunAsync();return;}

static void Check(bool condition,string message) {if(!condition) throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
var settings=AppSettings.Normalize(new AppSettings {speech = new SpeechSettings{rate=99,volume=-12}, magnifier=new MagnifierSettings{zoom=30}});
Check(settings.speech.rate==10,"speech rate clamped");
Check(settings.speech.volume==0,"volume clamped");
Check(settings.magnifier.zoom==6.0,"zoom clamped");
var json=JsonSerializer.Serialize(settings,AppSettings.JsonOptions);
var loaded=AppSettings.Normalize(JsonSerializer.Deserialize<AppSettings>(json,AppSettings.JsonOptions)!);
Check(loaded.speech.rate==10 && loaded.version==1,"settings JSON round-trip");
var sampleIni=PortableIni.Write(new AppSettings {
    reading=new ReadingSettings{mode=ReadingModes.Manga,dwellMilliseconds=850,ocrFallback=false},
    speech=new SpeechSettings{rate=3,volume=55,voiceName="日本語 \"A=B\""},
    magnifier=new MagnifierSettings{zoom=2.5f},
    ocr=new OcrSettings{copyToClipboard=true,preferJapanese=false}
});
var parsedIni=PortableIni.Parse(sampleIni);
Check(parsedIni.reading.mode==ReadingModes.Manga && parsedIni.reading.dwellMilliseconds==850,"portable INI reading mode and dwell round trip");
Check(parsedIni.speech.voiceName=="日本語 \"A=B\"" && parsedIni.speech.rate==3 && parsedIni.speech.volume==55,"portable UTF-8 INI Japanese voice and settings round trip");
Check(parsedIni.ocr.copyToClipboard && !parsedIni.ocr.preferJapanese && parsedIni.magnifier.zoom==2.5f,"portable INI OCR and magnifier settings round trip");
Check(PortableIni.Parse("; comment\n[reading]\nmode=selection\nunknownKey=value\n").reading.mode==ReadingModes.Selection,"INI accepts comments and unknown keys");
Check(PortableIni.Parse("[reading]\ndwellMilliseconds=99999\nmode=invalid\n").reading.dwellMilliseconds==2000,"INI numeric limits enforced");
try {PortableIni.Parse("[app]\nversion=999\n");throw new Exception("FAIL: invalid INI version accepted");}
catch(InvalidDataException){Console.WriteLine("PASS: unsupported INI schema rejected");}
var japaneseIni=PortableIni.Write(new AppSettings {
    reading=new ReadingSettings{mode=ReadingModes.Off,dwellMilliseconds=1100,ocrFallback=false},
    speech=new SpeechSettings{rate=-7,volume=81,voiceName="日本語音声"},
    ocr=new OcrSettings{copyToClipboard=true,reuseLastRegion=true,preferJapanese=false},
    magnifier=new MagnifierSettings{zoom=3.5f}
});
Check(japaneseIni.Contains("[基本設定]") && japaneseIni.Contains("[読み取り動作]") &&
      japaneseIni.Contains("[音声読み上げ]") && japaneseIni.Contains("[文字認識]") &&
      japaneseIni.Contains("[画面拡大]"),"Japanese INI uses Japanese section headers");
Check(japaneseIni.Contains("読み上げ速度=-7") && japaneseIni.Contains("使用する音声名=") && japaneseIni.Contains("日本語音声") &&
      japaneseIni.Contains("画像の文字も読み取る=いいえ") &&
      japaneseIni.Contains("認識した文字をコピー=はい"),"Japanese INI uses readable keys and boolean words");
var parsedJapaneseIni=PortableIni.Parse(japaneseIni);
Check(parsedJapaneseIni.reading.mode==ReadingModes.Off && parsedJapaneseIni.speech.rate==-7 &&
      parsedJapaneseIni.speech.voiceName=="日本語音声" && parsedJapaneseIni.ocr.reuseLastRegion &&
      parsedJapaneseIni.magnifier.zoom==3.5f,"Japanese INI full round-trip");
Check(PortableIni.Parse("[音声読み上げ]\n読み上げ速度=－３\n音量=８０\n").speech.rate==-3,
    "Japanese INI allows fullwidth signed numeric speed");
Check(PortableIni.Parse("[読み取り動作]\n読み取りモード=漫画の吹き出し\n").reading.mode==ReadingModes.Manga,
    "Japanese manga mode maps to internal mode");
Check(PortableIni.Parse("[読み取り動作]\n読み取りモード=範囲選択のみ\n").reading.mode==ReadingModes.Selection,
    "Japanese selection mode maps to internal mode");
Check(PortableIni.Parse("[読み取り動作]\n画像の文字も読み取る=オフ\n").reading.ocrFallback==false,
    "Japanese on/off synonyms are readable");
var legacyIni=PortableIni.Parse("[reading]\nmode=off\ndwellMilliseconds=940\n[ocr]\ncopyToClipboard=true\n[speech]\nrate=6\n");
Check(legacyIni.reading.mode==ReadingModes.Off && legacyIni.reading.dwellMilliseconds==940 &&
      legacyIni.ocr.copyToClipboard && legacyIni.speech.rate==6,
    "legacy English INI values remain compatible");
Check(PortableIni.Write(legacyIni).Contains("読み上げ速度=6"),"legacy English INI is exported with Japanese keys");
Check(Path.GetFileName(AppSettings.SettingsFile)=="settings.ini","portable configuration path uses settings.ini beside exe");
try {AppSettings.Normalize(new AppSettings{version=99}); throw new Exception("FAIL: invalid schema accepted");}
catch(InvalidDataException) {Console.WriteLine("PASS: settings version rejected");}
using var bmp=new Bitmap(600,500);
using(var g=Graphics.FromImage(bmp)) {g.Clear(Color.Gray);g.FillEllipse(Brushes.White,130,90,250,180);g.DrawString("テスト",SystemFonts.DefaultFont,Brushes.Black,200,160);}
var area=BalloonDetector.Estimate(bmp,new Point(250,135));
Check(area.Contains(new Point(250,135)),"balloon contains tapped point");
Check(area.Width>=180 && area.Width<360 && area.Height>=140 && area.Height<280,"balloon detects connected light region");
var fallback=BalloonDetector.Estimate(bmp,new Point(30,30));
Check(fallback.Width>0 && fallback.Height>0,"fallback rectangle is valid");
var drag=SelectionGeometry.FromDrag(new Point(400,350),new Point(120,130),new Rectangle(0,0,600,400));
Check(drag==new Rectangle(120,130,280,220),"drag selection in either direction");
var edgeTap=SelectionGeometry.AroundTap(new Point(1,1),new Rectangle(0,0,600,400));
Check(edgeTap==new Rectangle(0,0,360,160),"touch tap near edge clipped into screen");
var smallTap=SelectionGeometry.AroundTap(new Point(120,40),new Rectangle(0,0,200,100));
Check(smallTap==new Rectangle(0,0,200,100),"touch tap fits small screen");
var negative=SelectionGeometry.ToScreen(new Rectangle(100,80,140,70),new Rectangle(-1920,-200,3840,1440));
Check(negative==new Rectangle(-1820,-120,140,70),"multi-monitor negative coordinate conversion");
Check(SelectionGeometry.FromDrag(new Point(-300,-300),new Point(-200,-200),new Rectangle(0,0,500,500)).IsEmpty,"selection outside screen is empty");
Check(!AppSettings.Normalize(new AppSettings {magnifier=new MagnifierSettings{enableTripleClick=true}}).magnifier.enableTripleClick,"legacy global triple click hook disabled");
var hoverRect=SelectionGeometry.AroundTap(new Point(430,210),new Rectangle(0,0,1200,900),440,180);
Check(hoverRect.Contains(new Point(430,210)) && hoverRect.Width==440 && hoverRect.Height==180,"hover text sample stays around cursor");
Check(SelectionGeometry.AroundTap(new Point(1198,899),new Rectangle(0,0,1200,900),440,180).Bottom==900,"hover region clips near desktop edge");
Check(new AppSettings().reading.mode==ReadingModes.Cursor,"default mode is automatic cursor reading");
Check(new AppSettings().reading.ocrFallback,"OCR fallback enabled by default");
Check(AppSettings.Normalize(new AppSettings{reading=new ReadingSettings{mode="strange",dwellMilliseconds=5}}).reading.mode==ReadingModes.Cursor,"invalid mode defaults to cursor");
Check(AppSettings.Normalize(new AppSettings{reading=new ReadingSettings{mode=ReadingModes.Manga,dwellMilliseconds=5000}}).reading.dwellMilliseconds==2000,"hover dwell bounded");
Check(ReadingModes.IsAuto(ReadingModes.Manga) && !ReadingModes.IsAuto(ReadingModes.Selection),"only cursor and manga are automatic");
Check(AppSettings.Normalize(new AppSettings{reading=new ReadingSettings{mode=ReadingModes.Off}}).reading.mode==ReadingModes.Off,"off mode is persistent");
using(var blank=new Bitmap(480,220)) {
    using(var g=Graphics.FromImage(blank))g.Clear(Color.White);
    Check(TextRegionDetector.Propose(blank,new Point(180,90)).Count==0,"no text regions invented from blank images");
}
using(var sample=new Bitmap(480,220)) {
    using(var g=Graphics.FromImage(sample)) {
        g.Clear(Color.White);
        using var f=new Font("Arial",26,FontStyle.Bold);
        g.DrawString("READ ME 123",f,Brushes.Black,new PointF(70,72));
    }
    var regions=TextRegionDetector.Propose(sample,new Point(160,90));
    Check(regions.Count>0,"edge-based candidate found around rendered text");
    Check(regions.Any(r=>r.Contains(new Point(145,94))),"proposed region overlaps text near cursor");
    using var enhanced=TextRegionDetector.EnhanceForOcr(sample);
    Check(enhanced.Width==960 && enhanced.Height==440,"candidate preprocessing enlarges legible pixels");
}
Check(JapaneseSpeechDiagnostic.Samples.Length==3,"Japanese local diagnostic has short medium and long samples");
var japaneseDiagnostic=JapaneseSpeechDiagnostic.Run();
Check(File.Exists(japaneseDiagnostic.SavedTo),"Japanese voice diagnostic writes a local report");
Check(japaneseDiagnostic.Report.Contains("日本語SAPI音声:"),"Japanese voice diagnostic reports voice availability explicitly");
SpeechDispatchTests.Run();
Check(TrayReadingPolicy.CanAutomaticallyRead(false,false,ReadingModes.Cursor),
    "tray stays active: default cursor automatic reading works with settings hidden");
Check(TrayReadingPolicy.CanAutomaticallyRead(false,false,ReadingModes.Manga),
    "tray stays active: manga hover OCR works with settings hidden");
Check(!TrayReadingPolicy.CanAutomaticallyRead(true,false,ReadingModes.Cursor),
    "opening settings pauses hover reading to prevent unwanted speech");
Check(!TrayReadingPolicy.CanAutomaticallyRead(false,true,ReadingModes.Cursor),
    "manual selection pauses automatic hover reading");
Check(!TrayReadingPolicy.CanAutomaticallyRead(false,false,ReadingModes.Selection) &&
      !TrayReadingPolicy.CanAutomaticallyRead(false,false,ReadingModes.Off),
    "selection-only and off modes do not trigger cursor reading");
int diagnosticEvents=0;
void OnDiagnosticEntry(string _) => diagnosticEvents++;
DiagnosticLog.EntryAdded+=OnDiagnosticEntry;
DiagnosticLog.Info("テスト","記録の動作を確認しました。個人情報やOCRの文章は出力しません。");
DiagnosticLog.EntryAdded-=OnDiagnosticEntry;
Check(Path.GetFileName(DiagnosticLog.FilePath)=="ScreenReaderAssist.log",
    "diagnostic log is beside executable");
Check(File.Exists(DiagnosticLog.FilePath),"diagnostic log file is created locally");
Check(diagnosticEvents==1 && DiagnosticLog.Snapshot().Contains("[テスト]"),
    "diagnostics stream updates live status listeners");
Check(!string.IsNullOrEmpty(DiagnosticLog.Snapshot()),"diagnostic snapshot is available");
Check(SelectionCues.StartWaveData.Length>44 && SelectionCues.EndWaveData.Length>44,
    "selection start and end confirmation cues contain WAV audio data");
Check(!SelectionCues.StartWaveData.Span.SequenceEqual(SelectionCues.EndWaveData.Span),
    "selection start and end tones have different waveforms");
var cueWave=SelectionCues.StartWaveData.ToArray();
Check(System.Text.Encoding.ASCII.GetString(cueWave,0,4)=="RIFF" &&
      System.Text.Encoding.ASCII.GetString(cueWave,8,4)=="WAVE" &&
      BitConverter.ToInt32(cueWave,24)==22050,
    "selection cue has a valid RIFF WAV header and 22050 Hz sample rate");
Check(cueWave.Length<15000,
    "selection cue is short and does not require loading an audio file");
Console.WriteLine("Finished all logic smoke tests. OCR and live GUI tests NOT executed by this test suite.");
