using ScreenReaderAssist;
using System.Drawing;
using System.Text.Json;

static void Check(bool condition,string message) {if(!condition) throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
var settings=AppSettings.Normalize(new AppSettings {speech = new SpeechSettings{rate=99,volume=-12}, magnifier=new MagnifierSettings{zoom=30}});
Check(settings.speech.rate==10,"speech rate clamped");
Check(settings.speech.volume==0,"volume clamped");
Check(settings.magnifier.zoom==6.0,"zoom clamped");
var json=JsonSerializer.Serialize(settings,AppSettings.JsonOptions);
var loaded=AppSettings.Normalize(JsonSerializer.Deserialize<AppSettings>(json,AppSettings.JsonOptions)!);
Check(loaded.speech.rate==10 && loaded.version==1,"settings JSON round-trip");
try {AppSettings.Normalize(new AppSettings{version=99}); throw new Exception("FAIL: invalid schema accepted");}
catch(InvalidDataException) {Console.WriteLine("PASS: settings version rejected");}
using var bmp=new Bitmap(600,500);
using(var g=Graphics.FromImage(bmp)) {g.Clear(Color.Gray);g.FillEllipse(Brushes.White,130,90,250,180);g.DrawString("テスト",SystemFonts.DefaultFont,Brushes.Black,200,160);}
var area=BalloonDetector.Estimate(bmp,new Point(250,135));
Check(area.Contains(new Point(250,135)),"balloon contains tapped point");
Check(area.Width>=180 && area.Width<360 && area.Height>=140 && area.Height<280,"balloon detects connected light region");
var fallback=BalloonDetector.Estimate(bmp,new Point(30,30));
Check(fallback.Width>0 && fallback.Height>0,"fallback rectangle is valid");
Console.WriteLine("Finished all logic smoke tests. OCR and live GUI tests NOT executed by this test suite.");
