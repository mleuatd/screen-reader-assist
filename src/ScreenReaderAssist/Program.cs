namespace ScreenReaderAssist;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var singleton = new Mutex(true, "Local\\ScreenReaderAssist-SingleInstance", out bool first);
        if (!first) { MessageBox.Show("アプリはすでに起動しています。", "Screen Reader Assist"); return; }
        Application.Run(new MainForm());
    }
}
