using System.Text.Json;
namespace ScreenReaderAssist;

public sealed class AppSettings
{
    public int version { get; set; } = 1;
    public SpeechSettings speech { get; set; } = new();
    public OcrSettings ocr { get; set; } = new();
    public MagnifierSettings magnifier { get; set; } = new();
    public ReadingSettings reading { get; set; } = new();
    // Portable extraction keeps settings.ini beside the EXE.
    // An installation under Program Files uses a per-user writable directory.
    public static string SettingsFile => ResolveSettingsFile(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string ResolveSettingsFile(
        string applicationDir, string programFiles, string programFilesX86, string localAppData)
    {
        static bool IsWithin(string directory, string root)
        {
            if(string.IsNullOrWhiteSpace(root))return false;
            string basePath=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string appPath=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return appPath.StartsWith(basePath,StringComparison.OrdinalIgnoreCase);
        }
        if(IsWithin(applicationDir,programFiles)||IsWithin(applicationDir,programFilesX86))
            return Path.Combine(localAppData,"日本語拡大鏡","settings.ini");
        return Path.Combine(applicationDir,"settings.ini");
    }
    public static string LegacyJsonSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenReaderAssist","settings.json");
    public static JsonSerializerOptions JsonOptions { get; } = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static AppSettings Load()
    {
        if(File.Exists(SettingsFile))
            return PortableIni.Parse(File.ReadAllText(SettingsFile,System.Text.Encoding.UTF8));
        // On first run from Program Files, import the bundled defaults. Subsequent
        // saves go to the user's profile, never to the protected install folder.
        string bundledIni=Path.Combine(AppContext.BaseDirectory,"settings.ini");
        if(!String.Equals(SettingsFile,bundledIni,StringComparison.OrdinalIgnoreCase) &&
           File.Exists(bundledIni))
            return PortableIni.Parse(File.ReadAllText(bundledIni,System.Text.Encoding.UTF8));
        // Migration path for older previews. Never modify or delete the old JSON.
        if(File.Exists(LegacyJsonSettingsFile))
            return Normalize(JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(LegacyJsonSettingsFile),JsonOptions) ?? new());
        return new();
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
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
        config.reading.dwellMilliseconds=Math.Clamp(config.reading.dwellMilliseconds,200,2000);
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
    // Offline PP-OCRv6 is used when its model files are bundled; otherwise Windows OCR.
    public bool highAccuracyLocal { get; set; } = true;
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
    // Older installs might still store mode=cursor. Never enable unsolicited
    // hover speech unless the user explicitly opts in via this separate flag.
    public bool autoReadEnabled { get; set; } = false;
    public string mode { get; set; } = ReadingModes.Selection;
    public int dwellMilliseconds { get; set; } = 300;
    public bool ocrFallback { get; set; } = true;
}
