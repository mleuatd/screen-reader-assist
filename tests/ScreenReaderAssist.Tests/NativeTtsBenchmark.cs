using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text;

internal static class NativeTtsBenchmark
{
    public static void Run()
    {
        Console.OutputEncoding=Encoding.UTF8;
        try
        {
            using var synth=new SpeechSynthesizer();
            var installed=synth.GetInstalledVoices().Where(x=>x.Enabled).ToArray();
            if(installed.Length==0)
            {
                Console.WriteLine("TTS BENCH SKIP: no enabled SAPI voices on this Windows runner");
                return;
            }
            var selected=installed.FirstOrDefault(x=>x.VoiceInfo.Culture.TwoLetterISOLanguageName=="ja")
                ?? installed[0];
            synth.SelectVoice(selected.VoiceInfo.Name);
            synth.Rate=0;
            synth.Volume=100;
            bool japanese=selected.VoiceInfo.Culture.TwoLetterISOLanguageName=="ja";
            var brief=japanese ? "認識した文章を音声で読み上げる性能を測定します。" :
                "This benchmark measures how quickly Windows creates speech audio from recognized text.";
            Console.WriteLine("TTS BENCH native SAPI voice="+selected.VoiceInfo.Name+"; culture="+selected.VoiceInfo.Culture.Name);
            for(int i=0;i<3;i++)
            {
                var text=i==2?brief+" "+brief+" "+brief:brief;
                using var output=new MemoryStream();
                synth.SetOutputToWaveStream(output);
                var watch=Stopwatch.StartNew();
                synth.Speak(text);
                watch.Stop();
                Console.WriteLine($"TTS BENCH native render run={i+1}, chars={text.Length}, generate_ms={watch.Elapsed.TotalMilliseconds:F1}, output_bytes={output.Length}, chars_per_second={text.Length/Math.Max(.001,watch.Elapsed.TotalSeconds):F1}");
                if(output.Length<44)throw new InvalidOperationException("SAPI produced an unexpectedly short WAV");
            }
            Console.WriteLine("TTS BENCH complete. Measurements are offline waveform synthesis time, NOT audio playback delay.");
        }
        catch(Exception ex)
        {
            // CI may lack a speech synthesis engine or use a non-interactive session.
            Console.WriteLine("TTS BENCH UNAVAILABLE: "+ex.GetType().Name+": "+ex.Message);
        }
    }
}
