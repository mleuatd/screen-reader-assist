using System.Globalization;
using System.Text;

namespace ScreenReaderAssist;

/// <summary>
/// Offline, user-local handoff of SAPI speech rate between the separate
/// magnifier PowerShell host and the .NET 10 tray reader.
/// A single integer (-10..10) is shared; no OCR text or screen data is stored.
/// </summary>
public static class SharedSpeechRate
{
    public static string PathName => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenReaderAssist", "voice-rate.shared");

    public static bool TryRead(out int rate, string? path=null)
    {
        rate=0;
        try
        {
            var value=File.ReadAllText(path??PathName,Encoding.ASCII).Trim();
            if(!int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var parsed)
               || parsed < -10 || parsed > 10)return false;
            rate=parsed;
            return true;
        }
        catch(IOException) {return false;}
        catch(UnauthorizedAccessException) {return false;}
    }

    public static bool Publish(int rate, string? path=null)
    {
        rate=Math.Clamp(rate,-10,10);
        var destination=path??PathName;
        string? temp=null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temp,rate.ToString(CultureInfo.InvariantCulture),Encoding.ASCII);
            File.Move(temp,destination,true);
            return true;
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
        finally {if(temp!=null){try{File.Delete(temp);}catch(IOException){}}}
    }
}
