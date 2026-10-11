using System.Diagnostics;
using ScreenReaderAssist;

internal static class SpeechDispatchTests
{
    public static void Run()
    {
        CheckVoiceAndConfigurationCache();
        CheckVoiceSwitchAndFallback();
        CheckPauseStopAndEmpty();
        BenchmarkDispatch();
    }

    private static void Assert(bool valid,string message)
    {
        if(!valid)throw new Exception("FAIL: "+message);
        Console.WriteLine("PASS: "+message);
    }

    private static void CheckVoiceAndConfigurationCache()
    {
        var engine=new FakeEngine();
        using(var speech=new SpeechService(engine))
        {
            var settings=new SpeechSettings {rate=2,volume=83};
            for(int i=0;i<1000;i++)speech.Speak("短い文章の連続",settings);
            Assert(engine.VoiceReads==1,"TTS voice inventory enumerated only once");
            Assert(engine.VoiceSelects==1,"TTS Japanese voice selected only once for 1000 requests");
            Assert(engine.RateWrites==1 && engine.VolumeWrites==1,"TTS unchanged rate and volume applied once");
            Assert(engine.SpeakCalls==1000,"TTS dispatches all 1000 different requests without extra configuration");
            Assert(engine.LastText=="短い文章の連続","TTS preserves Japanese text");
            var names=speech.VoiceNames;
            names[0]="modified";
            Assert(speech.VoiceNames[0]=="English","TTS protects cached voice inventory");
        }
        Assert(engine.DisposeCalls==1,"TTS native engine disposed exactly once");
    }

    private static void CheckVoiceSwitchAndFallback()
    {
        var e=new FakeEngine();
        using var speech=new SpeechService(e);
        speech.Speak("one",new SpeechSettings{voiceName="English",rate=-1,volume=100});
        Assert(e.VoiceSelects==0,"TTS default English voice does not require select");
        speech.Speak("二",new SpeechSettings{voiceName="Japanese",rate=-1,volume=100});
        speech.Speak("三",new SpeechSettings{voiceName="Japanese",rate=-1,volume=100});
        Assert(e.VoiceSelects==1,"TTS voice change takes effect once");
        speech.Speak("四",new SpeechSettings{voiceName="non-existent",rate=5,volume=91});
        Assert(e.CurrentVoice=="Japanese" && e.VoiceSelects==1,"TTS removed voices fall back to Japanese without reselection");
        Assert(e.RateWrites==2 && e.VolumeWrites==1,"TTS only changed rate and volume are reapplied");
    }

    private static void CheckPauseStopAndEmpty()
    {
        var e=new FakeEngine();
        var service=new SpeechService(e);
        service.Speak("  ",new SpeechSettings());
        Assert(e.CancelCalls==0 && e.SpeakCalls==0,"TTS blank input performs no native work");
        e.Paused=true;
        service.Speak("resume",new SpeechSettings());
        Assert(e.ResumeCalls==1 && !e.Paused,"TTS new text resumes paused playback before speaking");
        service.Stop();
        Assert(e.CancelCalls==2,"TTS Stop cancels current playback");
        service.Dispose();
        service.Dispose();
        Assert(e.DisposeCalls==1,"TTS Dispose is idempotent");
    }

    private static void BenchmarkDispatch()
    {
        var e=new FakeEngine();
        using var service=new SpeechService(e);
        var settings=new SpeechSettings{rate=0,volume=100};
        const int requests=10000;
        var timer=Stopwatch.StartNew();
        // The benchmark measures only dispatch cost; exclude file-system diagnostic work.
        DiagnosticLog.Enabled=false;
        try {for(int i=0;i<requests;i++)service.Speak("Recognized text for fast speech dispatch",settings);}
        finally {DiagnosticLog.Enabled=true;}
        timer.Stop();
        Assert(e.SpeakCalls==requests,"TTS synthetic 10000-request dispatch count");
        Assert(e.RateWrites==0 && e.VolumeWrites==0,"TTS default settings need no native property writes");
        Console.WriteLine($"BENCH Dispatch only (fake engine; NOT audio generation): {requests} requests / {timer.Elapsed.TotalMilliseconds:F2} ms; 1 voice scan; {e.VoiceSelects} voice selections; {e.RateWrites} rate writes; {e.VolumeWrites} volume writes");
    }

    private sealed class FakeEngine : ISpeechEngine
    {
        private readonly SpeechVoice[] _voices={new("English",false),new("Japanese",true)};
        private string _selected="English";
        private int _rate,_volume=100;
        public int VoiceReads,VoiceSelects,RateWrites,VolumeWrites,SpeakCalls,CancelCalls,ResumeCalls,DisposeCalls;
        public string LastText="";
        public bool Paused;
        public IReadOnlyList<SpeechVoice> Voices {get {VoiceReads++;return _voices;}}
        public string CurrentVoice=>_selected;
        public int Rate {get=>_rate;set {_rate=value;RateWrites++;}}
        public int Volume {get=>_volume;set {_volume=value;VolumeWrites++;}}
        public bool IsPaused=>Paused;
        public void SelectVoice(string voice) {VoiceSelects++;_selected=voice;}
        public void CancelAll(){CancelCalls++;}
        public void SpeakAsync(string text){SpeakCalls++;LastText=text;}
        public void Pause(){Paused=true;}
        public void Resume(){Paused=false;ResumeCalls++;}
        public void Dispose(){DisposeCalls++;}
    }
}
