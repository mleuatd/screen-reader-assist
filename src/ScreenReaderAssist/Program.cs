namespace ScreenReaderAssist;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        DiagnosticLog.Info("起動","プロセス開始。実行フォルダーからログ記録を開始します。");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>DiagnosticLog.Error("UIスレッド例外",e.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>
        {
            if(e.ExceptionObject is Exception ex) DiagnosticLog.Error("未処理例外",ex);
            else DiagnosticLog.Warn("未処理例外","Exception以外の未処理エラー");
        };
        TaskScheduler.UnobservedTaskException+=(_,e)=>
        {
            DiagnosticLog.Error("非同期処理の未観測例外",e.Exception);
            e.SetObserved();
        };
        try
        {
            ApplicationConfiguration.Initialize();
            using var singleton=new Mutex(true,"Local\\ScreenReaderAssist-SingleInstance",out bool first);
            if(!first)
            {
                DiagnosticLog.Warn("二重起動","別プロセスが既に起動しています。追加のEXEは終了します。");
                MessageBox.Show("アプリはすでに起動しています。時計の近くの通知領域にあるアイコンから設定を開けます。",
                    "Screen Reader Assist");
                return;
            }
            DiagnosticLog.Info("起動","単一起動確認成功。設定と音声エンジンを初期化します。");
            Application.Run(new MainForm());
            DiagnosticLog.Info("終了","メッセージループを終了しました。");
        }
        catch(Exception ex)
        {
            DiagnosticLog.Error("起動失敗",ex);
            MessageBox.Show("起動エラーが発生しました。EXEと同じフォルダーのScreenReaderAssist.logを確認してください。",
                "Screen Reader Assist");
        }
    }
}
