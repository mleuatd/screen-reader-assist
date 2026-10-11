using System.Text;

namespace ScreenReaderAssist;

/// <summary>
/// Local-only diagnostics, deliberately excluding recognized text and screen images.
/// Stored beside the portable executable; never uploaded anywhere.
/// </summary>
public static class DiagnosticLog
{
    private const long MaxBytes=2*1024*1024;
    private static readonly object Gate=new();
    private static readonly Queue<string> Recent=new();
    private static string? _lastWriteError;

    public static string FilePath=>Path.Combine(AppContext.BaseDirectory,"ScreenReaderAssist.log");
    public static string BackupPath=>Path.Combine(AppContext.BaseDirectory,"ScreenReaderAssist.previous.log");
    public static string? LastWriteError { get { lock(Gate) return _lastWriteError; } }
    public static event Action<string>? EntryAdded;

    public static void Info(string category,string message)=>Append("情報",category,message);
    public static void Warn(string category,string message)=>Append("注意",category,message);
    public static void Error(string category,Exception exception)=>
        Append("エラー",category,exception.ToString());

    private static void Append(string level,string category,string message)
    {
        // Caller must not pass OCR content, copied text, full URLs, or screenshots.
        var safe=message.Replace("\r"," ").Replace("\n"," | ");
        var line=$"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] [{category}] {safe}";
        Action<string>? notify;
        lock(Gate)
        {
            Recent.Enqueue(line);
            while(Recent.Count>160)Recent.Dequeue();
            try
            {
                var file=FilePath;
                if(File.Exists(file) && new FileInfo(file).Length>=MaxBytes)
                {
                    File.Move(file,BackupPath,true);
                }
                File.AppendAllText(file,line+Environment.NewLine,new UTF8Encoding(false));
                _lastWriteError=null;
            }
            catch(Exception ex)
            {
                _lastWriteError=$"{ex.GetType().Name}: {ex.Message}";
                // Do not crash a voice accessibility tool if folder is read-only.
            }
            notify=EntryAdded;
        }
        try {notify?.Invoke(line);} catch { /* GUI may be shutting down. */ }
    }

    public static string Snapshot()
    {
        lock(Gate)return string.Join(Environment.NewLine,Recent);
    }
}
