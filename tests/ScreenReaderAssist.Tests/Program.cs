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
Console.WriteLine("Finished all logic smoke tests. OCR and live GUI tests NOT executed by this test suite.");
