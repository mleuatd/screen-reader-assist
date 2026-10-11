using System.Drawing;
using System.Media;
using System.Text;

namespace ScreenReaderAssist;

/// <summary>
/// Short, distinctive local PCM notification cues for starting and ending
/// Ctrl+Alt+R selection. No SAPI voice or external file is required.
/// </summary>
public static class SelectionCues
{
    private const int SampleRate=22050;
    private const int ToneMilliseconds=80;
    private const int SilenceMilliseconds=25;

    private static readonly byte[] StartWave=CreateWave(new[]{660,990});
    private static readonly byte[] EndWave=CreateWave(new[]{660,440});

    private static readonly object Gate=new();

    // Expose only WAV bytes for deterministic automated tests (no speaker required).
    public static ReadOnlyMemory<byte> StartWaveData=>StartWave;
    public static ReadOnlyMemory<byte> EndWaveData=>EndWave;

    public static void PlayStart()=>Play(StartWave,"開始");
    public static void PlayEnd()=>Play(EndWave,"終了");

    private static void Play(byte[] wave,string name)
    {
        try
        {
            // Fire-and-forget audio with separately owned streams so sound continues
            // while the selection overlay opens/closes. SoundPlayer.Play is nonblocking.
            lock(Gate)
            {
                using var stream=new MemoryStream(wave,writable:false);
                using var player=new SoundPlayer(stream);
                player.Load();
                player.Play();
            }
            DiagnosticLog.Info("操作音",$"{name}の確認音を再生要求しました。");
        }
        catch(Exception ex)
        {
            DiagnosticLog.Error("操作音"+name,ex);
            // A disabled sound device must never disable hotkeys or OCR.
        }
    }

    public static byte[] CreateWave(int[] frequencies)
    {
        if(frequencies is null || frequencies.Length==0 || frequencies.Length>6 ||
           frequencies.Any(f=>f<100 || f>3500))
            throw new ArgumentOutOfRangeException(nameof(frequencies));
        int samplesPerTone=SampleRate*ToneMilliseconds/1000;
        int silenceSamples=SampleRate*SilenceMilliseconds/1000;
        int sampleCount=frequencies.Length*samplesPerTone+(frequencies.Length-1)*silenceSamples;
        const int bytesPerSample=2;
        int dataLength=sampleCount*bytesPerSample;
        using var output=new MemoryStream(44+dataLength);
        using var writer=new BinaryWriter(output,Encoding.ASCII,leaveOpen:true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36+dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // signed little-endian PCM
        writer.Write((short)1); // mono
        writer.Write(SampleRate);
        writer.Write(SampleRate*bytesPerSample);
        writer.Write((short)bytesPerSample);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        for(int i=0;i<frequencies.Length;i++)
        {
            int frequency=frequencies[i];
            for(int sample=0;sample<samplesPerTone;sample++)
            {
                // Gentle attack and release; avoid clicks in headphones.
                double elapsed=(double)sample/SampleRate;
                double envelope=Math.Min(1.0,Math.Min(sample/220.0,(samplesPerTone-sample-1)/220.0));
                envelope=Math.Max(0.0,envelope);
                short amplitude=(short)Math.Round(0.17*short.MaxValue*envelope*
                    Math.Sin(2*Math.PI*frequency*elapsed));
                writer.Write(amplitude);
            }
            if(i+1<frequencies.Length)
                for(int sample=0;sample<silenceSamples;sample++)writer.Write((short)0);
        }
        writer.Flush();
        return output.ToArray();
    }
}
