using System.Speech.Synthesis;

namespace ScreenReaderAssist;

public sealed record SpeechVoice(string Name,bool IsJapanese);

/// <summary>Small injectable surface for testing speech dispatch without playing audio.</summary>
public interface ISpeechEngine : IDisposable
{
    IReadOnlyList<SpeechVoice> Voices { get; }
    string CurrentVoice { get; }
    int Rate { get; set; }
    int Volume { get; set; }
    bool IsPaused { get; }
    void SelectVoice(string name);
    void CancelAll();
    void SpeakAsync(string text);
    void Pause();
    void Resume();
}

/// <summary>
/// Keeps one native synthesizer and its voice list alive for the entire session.
/// No Windows voice enumeration or voice selection is needed per utterance.
/// </summary>
public sealed class SpeechService : IDisposable
{
    private readonly ISpeechEngine _engine;
    private readonly string[] _voiceNames;
    private readonly string _defaultVoice;
    private readonly string _initialVoice;
    private string _selectedVoice;
    private int? _appliedRate;
    private int? _appliedVolume;
    private bool _disposed;

    public SpeechService():this(new WindowsSpeechEngine()) { }

    public SpeechService(ISpeechEngine engine)
    {
        _engine=engine;
        var voices=engine.Voices;
        _voiceNames=voices.Select(v=>v.Name).ToArray();
        _initialVoice=engine.CurrentVoice;
        _selectedVoice=_initialVoice;
        _appliedRate=engine.Rate;
        _appliedVolume=engine.Volume;
        _defaultVoice=voices.FirstOrDefault(v=>v.IsJapanese)?.Name ?? _initialVoice;
        DiagnosticLog.Info("音声初期化",$"音声エンジン準備完了。音声数={voices.Count}、日本語音声数={voices.Count(v=>v.IsJapanese)}。");
    }

    public string[] VoiceNames => (string[])_voiceNames.Clone();

    public void Speak(string text,SpeechSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(string.IsNullOrWhiteSpace(text))return;
        DiagnosticLog.Info("音声要求",$"読み上げ要求を受信。文字数={text.Length}。");

        // Stop an old utterance before changing voice or speaking the next one.
        // CancelAll also clears queued playback, so the latest requested text wins.
        _engine.CancelAll();
        if(_engine.IsPaused)_engine.Resume();

        var wanted=ResolveVoice(settings.voiceName);
        if(!string.Equals(_selectedVoice,wanted,StringComparison.OrdinalIgnoreCase))
        {
            try {
                _engine.SelectVoice(wanted);
                _selectedVoice=wanted;
                DiagnosticLog.Info("音声選択","音声の選択を更新しました。");
            } catch(ArgumentException ex) {
                DiagnosticLog.Error("音声選択",ex);
                // Voices can be uninstalled while the tray process remains open.
                // Avoid retrying a stale voice on every utterance.
                _selectedVoice=_engine.CurrentVoice;
            }
        }
        int rate=Math.Clamp(settings.rate,-10,10);
        int volume=Math.Clamp(settings.volume,0,100);
        if(_appliedRate!=rate) { _engine.Rate=rate;_appliedRate=rate; }
        if(_appliedVolume!=volume) { _engine.Volume=volume;_appliedVolume=volume; }
        _engine.SpeakAsync(text);
        DiagnosticLog.Info("音声要求",$"音声エンジンへの非同期送信成功。速度={rate}、音量={volume}。");
    }

    private string ResolveVoice(string? configured)
    {
        if(string.IsNullOrWhiteSpace(configured))return _defaultVoice;
        foreach(var name in _voiceNames)
            if(string.Equals(name,configured,StringComparison.OrdinalIgnoreCase))return name;
        return _defaultVoice;
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        _engine.CancelAll();
        DiagnosticLog.Info("音声停止","音声停止を要求しました。");
    }

    public void TogglePause()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_engine.IsPaused){_engine.Resume();DiagnosticLog.Info("音声操作","一時停止から再開しました。");}
        else {_engine.Pause();DiagnosticLog.Info("音声操作","一時停止を要求しました。");}
    }

    public void Dispose()
    {
        if(_disposed)return;
        _disposed=true;
        _engine.Dispose();
    }
}

public sealed class WindowsSpeechEngine : ISpeechEngine
{
    private readonly SpeechSynthesizer _synth=new();
    private readonly SpeechVoice[] _voices;

    public WindowsSpeechEngine()
    {
        _synth.SetOutputToDefaultAudioDevice();
        _synth.SpeakStarted+=(_,_)=>DiagnosticLog.Info("音声再生","SAPIが読み上げを開始しました。");
        _synth.SpeakCompleted+=(_,e)=>{
            if(e.Error!=null)DiagnosticLog.Error("音声再生失敗",e.Error);
            else DiagnosticLog.Info("音声再生",e.Cancelled?"読み上げが取消されました。":"読み上げが完了しました。");
        };
        _voices=_synth.GetInstalledVoices()
            .Where(v=>v.Enabled)
            .Select(v=>new SpeechVoice(v.VoiceInfo.Name,
                v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("ja",StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public IReadOnlyList<SpeechVoice> Voices=>_voices;
    public string CurrentVoice=>_synth.Voice.Name;
    public int Rate {get=>_synth.Rate;set=>_synth.Rate=value;}
    public int Volume {get=>_synth.Volume;set=>_synth.Volume=value;}
    public bool IsPaused=>_synth.State==SynthesizerState.Paused;
    public void SelectVoice(string name)=>_synth.SelectVoice(name);
    public void CancelAll()=>_synth.SpeakAsyncCancelAll();
    public void SpeakAsync(string text)=>_synth.SpeakAsync(text);
    public void Pause() { if(_synth.State==SynthesizerState.Speaking) _synth.Pause(); }
    public void Resume() { if(_synth.State==SynthesizerState.Paused) _synth.Resume(); }
    public void Dispose()=>_synth.Dispose();
}
