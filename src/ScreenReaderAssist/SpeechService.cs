using System.Speech.Synthesis;
namespace ScreenReaderAssist;
public sealed class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _speech = new();
    public SpeechService() => _speech.SetOutputToDefaultAudioDevice();
    public string[] VoiceNames => _speech.GetInstalledVoices().Where(x=>x.Enabled).Select(x=>x.VoiceInfo.Name).ToArray();
    public void Speak(string text, SpeechSettings settings)
    {
        if(string.IsNullOrWhiteSpace(text)) return;
        _speech.SpeakAsyncCancelAll();
        _speech.Rate=settings.rate; _speech.Volume=settings.volume;
        if(!string.IsNullOrEmpty(settings.voiceName)) {
            try { _speech.SelectVoice(settings.voiceName); } catch (ArgumentException) { /* voice removed: use default */ }
        } else {
            var japanese=_speech.GetInstalledVoices().FirstOrDefault(x=>x.Enabled && x.VoiceInfo.Culture.Name.StartsWith("ja",StringComparison.OrdinalIgnoreCase));
            if (japanese is not null) _speech.SelectVoice(japanese.VoiceInfo.Name);
        }
        _speech.SpeakAsync(text);
    }
    public void Stop() => _speech.SpeakAsyncCancelAll();
    public void TogglePause()
    {
        if (_speech.State==SynthesizerState.Speaking) _speech.Pause();
        else if (_speech.State==SynthesizerState.Paused) _speech.Resume();
    }
    public void Dispose()=>_speech.Dispose();
}
