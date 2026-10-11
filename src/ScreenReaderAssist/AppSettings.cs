using System.Text.Json;
namespace ScreenReaderAssist;

public sealed class AppSettings
{
    public int version { get; set; } = 1;
    public SpeechSettings speech { get; set; } = new();
    public OcrSettings ocr { get; set; } = new();
    public MagnifierSettings magnifier { get; set; } = new();
    public ReadingSettings reading { get; set; } = new();
    // Portable first: this is located beside ScreenReaderAssist.exe, not in AppData.
    public static string SettingsFile => Path.Combine(AppContext.BaseDirectory,"settings.ini");
    public static string LegacyJsonSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenReaderAssist","settings.json");
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static AppSettings Load()
    {
        if(File.Exists(SettingsFile))
            return PortableIni.Parse(File.ReadAllText(SettingsFile,System.Text.Encoding.UTF8));
        // Migration path for older previews. Never modify or delete the old JSON.
        if(File.Exists(LegacyJsonSettingsFile))
            return Normalize(JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(LegacyJsonSettingsFile),JsonOptions) ?? new());
        return new();
    }
    public void Save()
    {
        var tmp=SettingsFile+".tmp";
        // Same-folder portable configuration. Do not silently save to another path.
        File.WriteAllText(tmp,PortableIni.Write(Normalize(this)),new System.Text.UTF8Encoding(false));
        File.Move(tmp,SettingsFile,true);
    }
    public static AppSettings Normalize(AppSettings config)
    {
        if (config.version != 1) throw new InvalidDataException("サポートされない設定バージョンです。");
        config.speech ??= new(); config.ocr ??= new(); config.magnifier ??= new(); config.reading ??= new();
        config.speech.rate = Math.Clamp(config.speech.rate, -10, 10);
        config.speech.volume = Math.Clamp(config.speech.volume, 0, 100);
        config.speech.voiceName = (config.speech.voiceName ?? "").Trim();
        config.magnifier.zoom = Math.Clamp(config.magnifier.zoom, 1.5f, 6.0f);
        // v0.2: retire the global click hook to avoid interfering with other applications.
        config.magnifier.enableTripleClick = false;
        config.reading.mode = ReadingModes.Normalize(config.reading.mode);
        config.reading.dwellMilliseconds=Math.Clamp(config.reading.dwellMilliseconds,400,2000);
        return config;
    }
}
public sealed class SpeechSettings
{
    public int rate { get; set; } = 0; // System.Speech API range -10 to +10, not a multiplier.
    public int volume { get; set; } = 100;
    public string voiceName { get; set; } = "";
}
public sealed class OcrSettings
{
    public bool copyToClipboard { get; set; } = false;
    public bool reuseLastRegion { get; set; } = false;
    public bool preferJapanese { get; set; } = true;
}
public sealed class MagnifierSettings
{
    public bool enableTripleClick { get; set; } = false;
    public float zoom { get; set; } = 2.0f;
}

public sealed class ReadingSettings
{
    public string mode { get; set; } = ReadingModes.Cursor;
    public int dwellMilliseconds { get; set; } = 750;
    public bool ocrFallback { get; set; } = true;
}
