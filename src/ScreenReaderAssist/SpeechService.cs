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
        _defaultVoice=voices.FirstOrDefault(v=>v.IsJapanese)?.Name ?? _initialVoice;
    }

    public string[] VoiceNames => (string[])_voiceNames.Clone();

    public void Speak(string text,SpeechSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(string.IsNullOrWhiteSpace(text))return;

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
            } catch(ArgumentException) {
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
    }

    public void TogglePause()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_engine.IsPaused)_engine.Resume();
        else _engine.Pause();
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
