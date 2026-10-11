using System.Diagnostics;
using System.Globalization;
using System.Speech.Synthesis;

internal static class JapaneseTtsBenchmark
{
    private static readonly string[] Samples =
    {
        "こんにちは。画面の文字を読み上げます。",
        "今日は二〇二六年十月十一日です。株価は三千五百円でした。午前十時三十分から始まります。",
        "この漫画のセリフは縦書きです。人物の会話と説明文を順序よく読むため、日本語の漢字、ひらがな、カタカナ、数字、句読点を含む文章を音声に変換します。難しい固有名詞の読み方は自動合成の結果と異なる場合があります。"
    };

    public static int Run()
    {
        Console.OutputEncoding=System.Text.Encoding.UTF8;
        try
        {
            using var synth=new SpeechSynthesizer();
            var voices=synth.GetInstalledVoices().Where(v=>v.Enabled).ToArray();
            foreach(var voice in voices)
                Console.WriteLine($"JA TTS INVENTORY name={voice.VoiceInfo.Name}, culture={voice.VoiceInfo.Culture.Name}, id={voice.VoiceInfo.Id}");
            var japanese=voices.Where(v=>v.VoiceInfo.Culture.TwoLetterISOLanguageName=="ja").ToArray();
            if(japanese.Length==0)
            {
                Console.WriteLine("JA TTS UNAVAILABLE: no enabled ja-JP SAPI voice; Japanese synthesis performance NOT measured.");
                return 2;
            }
            var measurements=0;
            foreach(var voice in japanese)
            {
                Console.WriteLine($"JA TTS BEGIN voice={voice.VoiceInfo.Name} culture={voice.VoiceInfo.Culture.Name}");
                synth.SelectVoice(voice.VoiceInfo.Name);
                synth.Volume=100;
                foreach(var rate in new[]{0,-2,3})
                {
                    synth.Rate=rate;
                    foreach(var (text,sampleId) in Samples.Select((t,i)=>(t,i+1)))
                    {
                        var elapsed=new List<double>();
                        long bytes=0;
                        // First warmup is measured separately because creating a voice can be costly.
                        for(int trial=0;trial<6;trial++)
                        {
                            using var output=new MemoryStream();
                            synth.SetOutputToWaveStream(output);
                            var watch=Stopwatch.StartNew();
                            synth.Speak(text);
                            watch.Stop();
                            if(output.Length<1024)
                                throw new InvalidOperationException("Japanese TTS output WAV is empty or too short.");
                            if(synth.Voice.Culture.TwoLetterISOLanguageName!="ja")
                                throw new InvalidOperationException("SpeechSynthesizer switched away from Japanese voice.");
                            elapsed.Add(watch.Elapsed.TotalMilliseconds);
                            bytes=output.Length;
                        }
                        var warmed=elapsed.Skip(1).OrderBy(n=>n).ToArray();
                        Console.WriteLine(
                            $"JA TTS RESULT voice={voice.VoiceInfo.Name}; rate={rate}; sample={sampleId}; chars={text.Length}; cold_ms={elapsed[0]:F1}; warm_median_ms={warmed[2]:F1}; warm_mean_ms={warmed.Average():F1}; warm_min_ms={warmed[0]:F1}; warm_max_ms={warmed[^1]:F1}; output_bytes={bytes}");
                        measurements++;
                    }
                }
            }
            Console.WriteLine($"JA TTS SUCCESS: {japanese.Length} Japanese voices, {measurements} voice/rate/sample combinations, 6 WAV render attempts each; no speakers or listening tested.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.WriteLine("JA TTS FAILED: "+ex);
            return 3;
        }
    }
}
