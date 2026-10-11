using System.Diagnostics;

internal static class OneCoreJapaneseBenchmark
{
    public static async Task<int> RunAsync()
    {
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        try
        {
            var voices=Windows.Media.SpeechSynthesis.SpeechSynthesizer.AllVoices.ToArray();
            foreach(var voice in voices)
                Console.WriteLine($"ONECORE INVENTORY name={voice.DisplayName},lang={voice.Language},id={voice.Id},gender={voice.Gender}");
            var japanese=voices.Where(v=>v.Language.StartsWith("ja",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(japanese.Length==0)
            {
                Console.WriteLine("ONECORE JA UNAVAILABLE: no Japanese Microsoft-signed WinRT voices installed");
                return 2;
            }
            using var synth=new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
            var shortText="おはようございます。画面に表示されている文字を読み上げます。";
            var longText="日本語の音声合成性能を測定します。ひらがな、カタカナ、漢字、記号、数字1234を含む文章を音声へ変換し、生成速度を測定しています。";
            int successes=0;
            foreach(var voice in japanese)
            {
                synth.Voice=voice;
                foreach(var (text,sample) in new[]{(shortText,"short"),(longText,"long")})
                {
                    for(int attempt=0;attempt<4;attempt++)
                    {
                        var sw=Stopwatch.StartNew();
                        using var stream=await synth.SynthesizeTextToStreamAsync(text);
                        sw.Stop();
                        if(stream.Size<1024)throw new InvalidOperationException("Synthesized OneCore stream has no audio.");
                        Console.WriteLine($"ONECORE JA RESULT voice={voice.DisplayName}; lang={voice.Language}; sample={sample}; attempt={attempt}; chars={text.Length}; synthesis_ms={sw.Elapsed.TotalMilliseconds:F1}; audio_bytes={stream.Size}");
                        successes++;
                    }
                }
            }
            Console.WriteLine($"ONECORE JA SUCCESS voices={japanese.Length}; samples={successes}; actual sound playback NOT measured");
            return 0;
        }
        catch(Exception ex)
        {
            Console.WriteLine("ONECORE JA FAILED: "+ex);
            return 3;
        }
    }
}
