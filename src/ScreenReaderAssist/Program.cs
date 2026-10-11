namespace ScreenReaderAssist;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        DiagnosticLog.Info("起動","プロセス開始。実行フォルダーからログ記録を開始します。");
        // Repair before the single-instance mutex and before constructing MainForm:
        // even a terminated/obsolete process can leave the fullscreen transform behind.
        bool explicitRepair=args.Any(x=>string.Equals(x,"--reset-display",StringComparison.OrdinalIgnoreCase));
        var recovery=explicitRepair?MagnificationRecovery.ExplicitReset():MagnificationRecovery.OnStartup();
        DiagnosticLog.Info("画面復旧",$"状態={recovery.Status}。{recovery.Detail}");
        if(explicitRepair)
        {
            ApplicationConfiguration.Initialize();
            MessageBox.Show(recovery.Successful?"画面表示の初期化を確認しました。":$"画面表示を初期化できませんでした。{recovery.Detail}",
                "画面表示の復旧");
            return;
        }
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
