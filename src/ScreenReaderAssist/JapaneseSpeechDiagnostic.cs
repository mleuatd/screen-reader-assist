using System.Diagnostics;
using System.Speech.Synthesis;
using System.Text;

namespace ScreenReaderAssist;

public sealed record JapaneseSpeechDiagnosticResult(bool HasJapaneseVoice,string Report,string SavedTo);

/// <summary>Tests every installed SAPI Japanese voice without playing audio.</summary>
public static class JapaneseSpeechDiagnostic
{
    public static readonly string[] Samples = {
        "こんにちは。文字を読み上げます。",
        "今日は二〇二六年十月十一日です。午前十時三十分、株価は三千五百円でした。",
        "画面に表示されている文章を音声で読み上げます。漢字、ひらがな、カタカナ、数字、句読点を混ぜても、正しい読み方になっているかは実際に耳で確認する必要があります。"
    };

    public static JapaneseSpeechDiagnosticResult Run()
    {
        var report=new StringBuilder();
        report.AppendLine("日本語音声のローカル性能診断");
        report.AppendLine("日時: "+DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        report.AppendLine("方式: System.Speech / Windows SAPI。スピーカーへ音声は流しません。");
        report.AppendLine("計測: WAVデータ合成の計算時間（ユーザーに音が届く時間ではありません）。");
        report.AppendLine();
        bool available=false;
        using(var synth=new SpeechSynthesizer())
        {
            var voices=synth.GetInstalledVoices().Where(v=>v.Enabled).ToArray();
            report.AppendLine("利用可能なSAPI音声一覧:");
            foreach(var v in voices)
                report.AppendLine("  "+v.VoiceInfo.Name+" ("+v.VoiceInfo.Culture.Name+")");
            var japanese=voices.Where(v=>v.VoiceInfo.Culture.TwoLetterISOLanguageName=="ja").ToArray();
            available=japanese.Length>0;
            report.AppendLine("日本語SAPI音声: "+japanese.Length+"種類");
            if(!available)
                report.AppendLine("未測定: 日本語SAPI音声が見つかりません。OneCore専用音声はこの方式では利用できません。");
            foreach(var voice in japanese)
            {
                report.AppendLine();
                report.AppendLine("音声: "+voice.VoiceInfo.Name);
                try
                {
                    synth.SelectVoice(voice.VoiceInfo.Name);
                    synth.Volume=100;
                    foreach(int rate in new[]{-2,0,3})
                    {
                        synth.Rate=rate;
                        for(int example=0;example<Samples.Length;example++)
                        {
                            var times=new List<double>();
                            long bytes=0;
                            for(int attempt=0;attempt<4;attempt++)
                            {
                                using var wave=new MemoryStream();
                                synth.SetOutputToWaveStream(wave);
                                var watch=Stopwatch.StartNew();
                                synth.Speak(Samples[example]);
                                watch.Stop();
                                if(wave.Length<1024)
                                    throw new InvalidOperationException("WAV出力が空です。");
                                times.Add(watch.Elapsed.TotalMilliseconds);
                                bytes=wave.Length;
                            }
                            var warm=times.Skip(1).OrderBy(x=>x).ToArray();
                            report.AppendLine($"  速度{rate:+0;-0;0}、文章{example+1}（{Samples[example].Length}文字）: 初回{times[0]:F1}ms、2回目以降の中央値{warm[1]:F1}ms、WAV {bytes:N0}バイト");
                        }
                    }
                } catch(Exception ex) {
                    report.AppendLine("  合成失敗: "+ex.GetType().Name+" / "+ex.Message);
                }
            }
        }
        report.AppendLine();
        report.AppendLine("注意：実音声の聴感、漢字の読み方、スピーカー再生開始時間は別途確認が必要です。");
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenReaderAssist","JapaneseVoiceDiagnostic.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        report.AppendLine("保存先: "+path);
        File.WriteAllText(path,report.ToString(),new UTF8Encoding(false));
        return new JapaneseSpeechDiagnosticResult(available,report.ToString(),path);
    }
}
