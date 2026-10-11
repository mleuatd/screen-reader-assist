using System.Globalization;
using System.Text;

namespace ScreenReaderAssist;

/// <summary>
/// Portable UTF-8 INI profile shared with the static website.
/// Unknown sections and keys are ignored; known numeric values use invariant culture.
/// </summary>
public static class PortableIni
{
    private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public static AppSettings Parse(string ini)
    {
        if(ini.Length>100_000)throw new InvalidDataException("設定ファイルが大きすぎます。");
        var config=new AppSettings();
        string section="";
        foreach(var line in ini.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
        {
            var t=line.Trim().TrimStart('\uFEFF');
            if(t.Length==0 || t.StartsWith('#') || t.StartsWith(';'))continue;
            if(t.StartsWith('[') && t.EndsWith(']'))
            {
                section=t[1..^1].Trim().ToLowerInvariant();
                continue;
            }
            int equals=t.IndexOf('=');
            if(equals<=0)continue;
            var key=t[..equals].Trim().ToLowerInvariant();
            var val=t[(equals+1)..].Trim();
            if(val.Length>=2 && val[0]=='"' && val[^1]=='"')
                val=val[1..^1].Replace("\"\"","\"");
            switch((section,key))
            {
                case ("app","version"):
                    if(int.TryParse(val,NumberStyles.Integer,Inv,out var version))config.version=version;
                    break;
                case ("speech","rate"):
                    if(int.TryParse(val,NumberStyles.Integer,Inv,out var rate))config.speech.rate=rate;
                    break;
                case ("speech","volume"):
                    if(int.TryParse(val,NumberStyles.Integer,Inv,out var vol))config.speech.volume=vol;
                    break;
                case ("speech","voicename"):config.speech.voiceName=val;break;
                case ("ocr","copytoclipboard"):
                    if(bool.TryParse(val,out var copy))config.ocr.copyToClipboard=copy;
                    break;
                case ("ocr","reuselastregion"):
                    if(bool.TryParse(val,out var reuse))config.ocr.reuseLastRegion=reuse;
                    break;
                case ("ocr","preferjapanese"):
                    if(bool.TryParse(val,out var jp))config.ocr.preferJapanese=jp;
                    break;
                case ("magnifier","zoom"):
                    if(float.TryParse(val,NumberStyles.Float,Inv,out var zoom))config.magnifier.zoom=zoom;
                    break;
                case ("magnifier","enabletripleclick"):
                    // Deprecated. Normalize always disables the global click hook.
                    if(bool.TryParse(val,out var triple))config.magnifier.enableTripleClick=triple;
                    break;
                case ("reading","mode"):config.reading.mode=val;break;
                case ("reading","dwellmilliseconds"):
                    if(int.TryParse(val,NumberStyles.Integer,Inv,out var ms))config.reading.dwellMilliseconds=ms;
                    break;
                case ("reading","ocrfallback"):
                    if(bool.TryParse(val,out var fallback))config.reading.ocrFallback=fallback;
                    break;
            }
        }
        return AppSettings.Normalize(config);
    }

    public static string Write(AppSettings input)
    {
        var s=AppSettings.Normalize(input);
        static string Bool(bool value)=>value?"true":"false";
        var safeVoice=(s.speech.voiceName??"").Replace("\r"," ").Replace("\n"," ").Replace("\"","\"\"");
        var sb=new StringBuilder();
        sb.AppendLine("; Screen Reader Assist - portable settings (UTF-8)");
        sb.AppendLine("; Place this file beside ScreenReaderAssist.exe. Edit with Notepad.");
        sb.AppendLine("[app]");
        sb.AppendLine("version=1");
        sb.AppendLine();
        sb.AppendLine("[reading]");
        sb.AppendLine("; cursor / manga / selection / off");
        sb.AppendLine("mode="+s.reading.mode);
        sb.AppendLine("dwellMilliseconds="+s.reading.dwellMilliseconds.ToString(Inv));
        sb.AppendLine("ocrFallback="+Bool(s.reading.ocrFallback));
        sb.AppendLine();
        sb.AppendLine("[speech]");
        sb.AppendLine("rate="+s.speech.rate.ToString(Inv));
        sb.AppendLine("volume="+s.speech.volume.ToString(Inv));
        sb.AppendLine("voiceName=\""+safeVoice+"\"");
        sb.AppendLine();
        sb.AppendLine("[ocr]");
        sb.AppendLine("copyToClipboard="+Bool(s.ocr.copyToClipboard));
        sb.AppendLine("reuseLastRegion="+Bool(s.ocr.reuseLastRegion));
        sb.AppendLine("preferJapanese="+Bool(s.ocr.preferJapanese));
        sb.AppendLine();
        sb.AppendLine("[magnifier]");
        sb.AppendLine("zoom="+s.magnifier.zoom.ToString(Inv));
        return sb.ToString();
    }
}
