using System.Globalization;
using System.Text;

namespace ScreenReaderAssist;

/// <summary>UTF-8 INI file with user-facing Japanese section names and keys.
/// Legacy English INI files remain readable for upgrades.</summary>
public static class PortableIni
{
    private static readonly CultureInfo Inv=CultureInfo.InvariantCulture;

    private static string NormalizeNumber(string text)=>
        text.Normalize(NormalizationForm.FormKC);

    private static bool TryToggle(string text,out bool enabled)
    {
        switch(text.Trim().ToLowerInvariant())
        {
            case "はい":case "オン":case "有効":case "true":case "1":case "yes":
                enabled=true;return true;
            case "いいえ":case "オフ":case "無効":case "false":case "0":case "no":
                enabled=false;return true;
            default:enabled=false;return false;
        }
    }

    private static string ParseMode(string value)=>value.Trim() switch
    {
        "カーソルで自動読み上げ" or "カーソル自動読み上げ" or "カーソル" => ReadingModes.Cursor,
        "漫画の吹き出し" or "漫画" => ReadingModes.Manga,
        "範囲選択のみ" or "範囲選択" => ReadingModes.Selection,
        "自動読み上げ停止" or "停止" => ReadingModes.Off,
        _ => ReadingModes.Normalize(value)
    };
    private static string WriteMode(string? value)=>ReadingModes.Normalize(value) switch
    {
        ReadingModes.Manga=>"漫画の吹き出し",
        ReadingModes.Selection=>"範囲選択のみ",
        ReadingModes.Off=>"自動読み上げ停止",
        _=>"カーソルで自動読み上げ"
    };

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
                case ("基本設定","設定形式のバージョン"):
                case ("app","version"):
                    if(int.TryParse(NormalizeNumber(val),NumberStyles.Integer,Inv,out var version))config.version=version;
                    break;
                case ("音声読み上げ","読み上げ速度"):
                case ("speech","rate"):
                    if(int.TryParse(NormalizeNumber(val),NumberStyles.Integer,Inv,out var rate))config.speech.rate=rate;
                    break;
                case ("音声読み上げ","音量"):
                case ("speech","volume"):
                    if(int.TryParse(NormalizeNumber(val),NumberStyles.Integer,Inv,out var vol))config.speech.volume=vol;
                    break;
                case ("音声読み上げ","使用する音声名"):
                case ("speech","voicename"):
                    config.speech.voiceName=val;break;
                case ("文字認識","認識した文字をコピー"):
                case ("ocr","copytoclipboard"):
                    if(TryToggle(val,out var copy))config.ocr.copyToClipboard=copy;
                    break;
                case ("文字認識","前回の読み取り範囲を再利用"):
                case ("ocr","reuselastregion"):
                    if(TryToggle(val,out var reuse))config.ocr.reuseLastRegion=reuse;
                    break;
                case ("文字認識","日本語の文字認識を優先"):
                case ("ocr","preferjapanese"):
                    if(TryToggle(val,out var jp))config.ocr.preferJapanese=jp;
                    break;
                case ("画面拡大","拡大倍率"):
                case ("magnifier","zoom"):
                    if(float.TryParse(NormalizeNumber(val),NumberStyles.Float,Inv,out var zoom))config.magnifier.zoom=zoom;
                    break;
                case ("magnifier","enabletripleclick"):
                    // Retired feature: AppSettings.Normalize always disables it.
                    break;
                case ("読み取り動作","読み取りモード"):
                case ("reading","mode"):
                    config.reading.mode=ParseMode(val);break;
                case ("読み取り動作","カーソル停止時間（ミリ秒）"):
                case ("reading","dwellmilliseconds"):
                    if(int.TryParse(NormalizeNumber(val),NumberStyles.Integer,Inv,out var ms))config.reading.dwellMilliseconds=ms;
                    break;
                case ("読み取り動作","画像の文字も読み取る"):
                case ("reading","ocrfallback"):
                    if(TryToggle(val,out var fallback))config.reading.ocrFallback=fallback;
                    break;
            }
        }
        return AppSettings.Normalize(config);
    }

    public static string Write(AppSettings input)
    {
        var s=AppSettings.Normalize(input);
        static string Toggle(bool value)=>value?"はい":"いいえ";
        var safeVoice=(s.speech.voiceName??"").Replace("\r"," ").Replace("\n"," ").Replace("\"","\"\"");
        var sb=new StringBuilder();
        sb.AppendLine("; Screen Reader Assist の設定ファイル（UTF-8）");
        sb.AppendLine("; ScreenReaderAssist.exe と同じフォルダーに置いてください。");
        sb.AppendLine("; メモ帳で変更した場合、アプリを再起動すると反映されます。");
        sb.AppendLine("[基本設定]");
        sb.AppendLine("設定形式のバージョン=1");
        sb.AppendLine();
        sb.AppendLine("[読み取り動作]");
        sb.AppendLine("; 選択肢：カーソルで自動読み上げ／漫画の吹き出し／範囲選択のみ／自動読み上げ停止");
        sb.AppendLine("読み取りモード="+WriteMode(s.reading.mode));
        sb.AppendLine("; カーソルを止めてから読み始めるまでの時間（400～2000ミリ秒）");
        sb.AppendLine("カーソル停止時間（ミリ秒）="+s.reading.dwellMilliseconds.ToString(Inv));
        sb.AppendLine("画像の文字も読み取る="+Toggle(s.reading.ocrFallback));
        sb.AppendLine();
        sb.AppendLine("[音声読み上げ]");
        sb.AppendLine("; －10が最も遅く、0が標準、＋10が最も速い（21段階）");
        sb.AppendLine("読み上げ速度="+s.speech.rate.ToString(Inv));
        sb.AppendLine("; 0～100");
        sb.AppendLine("音量="+s.speech.volume.ToString(Inv));
        sb.AppendLine("; 空欄なら利用できる日本語音声を優先して選びます。");
        sb.AppendLine("使用する音声名=\""+safeVoice+"\"");
        sb.AppendLine();
        sb.AppendLine("[文字認識]");
        sb.AppendLine("認識した文字をコピー="+Toggle(s.ocr.copyToClipboard));
        sb.AppendLine("前回の読み取り範囲を再利用="+Toggle(s.ocr.reuseLastRegion));
        sb.AppendLine("日本語の文字認識を優先="+Toggle(s.ocr.preferJapanese));
        sb.AppendLine();
        sb.AppendLine("[画面拡大]");
        sb.AppendLine("拡大倍率="+s.magnifier.zoom.ToString(Inv));
        return sb.ToString();
    }
}
