using ScreenReaderAssist;

internal static class SharedSpeechRateTests
{
    public static void Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"SpeechRateSyncRegression_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path=Path.Combine(root,"voice-rate.shared");
        try
        {
            if(SharedSpeechRate.TryRead(out _,path))
                throw new Exception("Missing file must never claim a synchronized rate.");
            foreach(var rate in new[]{-10,-5,0,4,10})
            {
                if(!SharedSpeechRate.Publish(rate,path) ||
                    !SharedSpeechRate.TryRead(out var received,path) || received!=rate)
                    throw new Exception("Offline voice-rate handoff failed for rate "+rate);
            }
            SharedSpeechRate.Publish(100,path);
            if(!SharedSpeechRate.TryRead(out var clamped,path) || clamped!=10)
                throw new Exception("Speech rate must be clamped.");
            File.WriteAllText(path,"invalid-text");
            if(SharedSpeechRate.TryRead(out _,path))
                throw new Exception("Invalid shared rate should be ignored.");
            File.WriteAllText(path,"15");
            if(SharedSpeechRate.TryRead(out _,path))
                throw new Exception("Out-of-range shared rate should be ignored.");
            Console.WriteLine("PASS: 日本語読み上げ速度のローカル共有・上下限・異常データの回帰テスト成功。");
        }
        finally {
            try{Directory.Delete(root,true);}catch(IOException){}
        }
    }
}
