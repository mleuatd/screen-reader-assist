using System.Drawing;
using System.Text.Json;
namespace ScreenReaderAssist;

public sealed class MainForm : Form
{
    private AppSettings _settings;
    private readonly SpeechService _speech = new();
    private readonly OcrService _ocr = new();
    // Fullscreen magnification disabled: forcibly killing an app leaves the desktop transformed.
    private readonly NotifyIcon _tray;
    private readonly NumericUpDown _rate = new() { Minimum=-10, Maximum=10, Width=80 };
    private readonly NumericUpDown _volume = new() { Minimum=0, Maximum=100, Width=80 };
    private readonly NumericUpDown _zoom = new() { Minimum=15, Maximum=60, Increment=5, Width=80, Enabled=false };
    private readonly ComboBox _voice = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=330 };
    private readonly CheckBox _triple = new() { Text="危険な全画面拡大は一時停止中。Alt+Shift+Zは画面の復旧用", AutoSize=true, Enabled=false };
    private readonly CheckBox _copy = new() { Text="認識した文字をクリップボードへコピー", AutoSize=true };
    private readonly CheckBox _japanese = new() { Text="日本語OCRを優先", AutoSize=true };
    private readonly Label _status = new() { Text="準備完了", AutoSize=false, Width=540, Height=60, AccessibleName="現在の状態", AutoEllipsis=true };
    private readonly TextBox _last = new() { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, Height=110, Dock=DockStyle.Fill, AccessibleName="最後に認識した文章" };
    private Rectangle? _lastRect;
    private bool _busy,_quitting,_diagnosingJapanese;
    private bool _settingsMayBeShown;
    private readonly SemaphoreSlim _hoverGate = new(1,1);
    private string _lastHoverSpoken="";
    private string _lastText="";
    private readonly System.Windows.Forms.Timer _cursorTimer = new() { Interval=100 };
    private readonly ComboBox _readingMode = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=340 };
    private readonly NumericUpDown _hoverDelay = new() { Minimum=400,Maximum=2000,Increment=100,Width=100 };
    private readonly CheckBox _ocrFallback = new() { Text="デジタル文字がなければ画像OCRも試す",AutoSize=true };
    private Point _lastPointer=new(-100000,-100000);
    private long _pointerMovedAt;
    private bool _hoverBusy,_hoverAttempted,_loadingMode;
    private int _cursorGeneration;
    private string _lastAutoText="";
    private readonly ToolStripMenuItem _modeMenu = new("読み上げモード");
    private readonly TextBox _diagnosticEvents = new() {
        Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,
        Dock=DockStyle.Fill,AccessibleName="リアルタイム診断ログ。起動、ショートカット、OCR、音声再生の状態"
    };
    private readonly Label _health = new() {
        AutoSize=true,Text="診断状態を確認しています…",AccessibleName="現在の動作状態"
    };
    private readonly System.Windows.Forms.Timer _healthTimer = new() { Interval=1000 };
    private int _heartbeatCounter;
    private bool _hotkeyRRegistered;

    public MainForm()
    {
        try {
            _settings=AppSettings.Load();
            DiagnosticLog.Info("設定",$"設定ファイルを読み込みました。読み上げモード={ReadingModes.Label(_settings.reading.mode)}、待機={_settings.reading.dwellMilliseconds}ms。");
        } catch(Exception ex) {
            DiagnosticLog.Error("設定読み込み",ex);
            _settings=new AppSettings();
        }
        Text="Screen Reader Assist 設定";
        AccessibleName="Screen Reader Assist 設定画面";
        MinimumSize=new Size(660,610);
        Size=new Size(720,660);
        StartPosition=FormStartPosition.CenterScreen;
        FormClosing+=(_,e)=>{
            // Only an intentional settings-window close hides it. Windows shutdown still exits.
            if(!_quitting && e.CloseReason==CloseReason.UserClosing) {
                e.Cancel=true;
                HideSettings();
            }
        };
        _tray=new NotifyIcon { Icon=SystemIcons.Information, Visible=true, Text="Screen Reader Assist" };
        var menu=new ContextMenuStrip { AccessibleName="Screen Reader Assist メニュー" };
        menu.Items.Add("設定を開く",null,(_,_)=>OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        foreach(var code in new[]{ReadingModes.Cursor,ReadingModes.Manga,ReadingModes.Selection,ReadingModes.Off}) {
            var selected=code;
            _modeMenu.DropDownItems.Add(ReadingModes.Label(code),null,(_,_)=>SetMode(selected));
        }
        menu.Items.Add(_modeMenu);
        menu.Items.Add("範囲を読み上げ",null,async (_,_)=>await ReadSelection());
        menu.Items.Add("漫画の吹き出しを読む",null,async (_,_)=>await ReadBalloon());
        menu.Items.Add("注目中のUI要素を読む",null,(_,_)=>ReadFocused());
        menu.Items.Add("日本語音声の性能を検証",null,async (_,_)=>await DiagnoseJapaneseVoice());
        menu.Items.Add("動作ログを表示",null,(_,_)=>OpenDiagnosticLog());
        menu.Items.Add("画面の拡大表示を解除（復旧）",null,(_,_)=>ResetDisplayFromUser());
        menu.Items.Add("音声テスト",null,(_,_)=>{
            try {DiagnosticLog.Info("音声テスト","通知領域から直接音声を試験します。");_speech.Speak("これは読み上げ音声のテストです。",_settings.speech);}
            catch(Exception ex){DiagnosticLog.Error("音声テスト",ex);}
        });
        menu.Items.Add("読み上げ停止",null,(_,_)=>_speech.Stop());
        menu.Items.Add("終了",null,(_,_)=>Quit());
        _tray.ContextMenuStrip=menu;
        // Single left-click opens settings. Right-click displays the context menu.
        _tray.MouseClick+=(_,e)=> { if(e.Button==MouseButtons.Left) OpenSettings(); };
        _tray.DoubleClick+=(_,_)=>OpenSettings();
        BuildForm();
        DiagnosticLog.EntryAdded+=OnLogEntryAdded;
        _diagnosticEvents.Text=DiagnosticLog.Snapshot();
        _diagnosticEvents.SelectionStart=_diagnosticEvents.TextLength;
        _diagnosticEvents.ScrollToCaret();
        LoadControls();
        _cursorTimer.Tick+=CursorTimerTick;
        _pointerMovedAt=Environment.TickCount64;
        _cursorTimer.Start();
        _healthTimer.Tick+=(_,_)=>RefreshHealth();
        _healthTimer.Start();
        DiagnosticLog.Info("常駐","通知領域に常駐しました。設定画面は非表示でも監視を続けます。");
        if(DiagnosticLog.LastWriteError is string error) Status("EXE横にログを書き込めません: "+error);
        RefreshHealth();
        // Deliberately start hidden instead of briefly flashing a settings window.
        // The tray icon and cursor-reading timer remain active during normal use.
    }
    protected override void SetVisibleCore(bool value)
    {
        // Application.Run normally displays its main form. We keep its window handle
        // and message loop alive without ever displaying the settings on startup.
        if(value && !_settingsMayBeShown) {
            if(!IsHandleCreated) CreateHandle(); // Enables global hotkeys while hidden.
            value=false;
        }
        base.SetVisibleCore(value);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Register(1,Keys.R); Register(2,Keys.M); Register(3,Keys.S); Register(4,Keys.U); Register(5,Keys.P); Register(6,Keys.Z);
    }
    private void Register(int id,Keys k)
    {
        var modifiers = k==Keys.R ? NativeHotkeys.Control | NativeHotkeys.Alt : NativeHotkeys.Alt | NativeHotkeys.Shift;
        var description=k==Keys.R ? "Ctrl+Alt+R" : $"Alt+Shift+{k}";
        bool ok=NativeHotkeys.RegisterHotKey(Handle,id,modifiers | NativeHotkeys.NoRepeat,(int)k);
        if(k==Keys.R)_hotkeyRRegistered=ok;
        if(ok) DiagnosticLog.Info("ショートカット",$"{description} をWindowsに登録しました。");
        else {
            int code=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            DiagnosticLog.Warn("ショートカット",$"{description} の登録失敗。Windowsエラー={code}。他のアプリとのキー競合を確認してください。");
            _status.Text=$"ショートカット {description} が競合しています（Windowsエラー {code}）。";
        }
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        for(int i=1;i<=6;i++) NativeHotkeys.UnregisterHotKey(Handle,i);
        base.OnHandleDestroyed(e);
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==NativeHotkeys.WmHotkey) {
            DiagnosticLog.Info("ショートカット",$"Windowsからホットキー通知を受信しました。ID={m.WParam.ToInt32()}。");
            switch(m.WParam.ToInt32()) {
                case 1: _=ReadSelection(); break;
                case 2: _=ReadBalloon(); break;
                case 3: _speech.Stop(); break;
                case 4: ReadFocused(); break;
                case 5: _speech.TogglePause(); break;
                case 6: ToggleZoom(); break;
            }
        }
        base.WndProc(ref m);
    }
    private void BuildForm()
    {
        var panel=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),AutoScroll=true,ColumnCount=2,RowCount=17 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,185));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<16;i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Controls.Add(panel);
        int row=0;
        AddFull("通知領域で常駐します。設定画面の×を押しても自動読み上げは続きます。再表示：通知領域アイコンを左クリック、または右クリック→「設定を開く」。",row++);
        AddFull("Ctrl+Alt+R：範囲読み上げ。Alt+Shift+M：漫画、U：UI、S：停止、P：一時停止、Z：画面表示の復旧。危険な全画面拡大機能は停止中です。",row++);
        panel.Controls.Add(_health,0,row);
        panel.SetColumnSpan(_health,2);
        row++;
        Add("読み上げモード",_readingMode,row++);
        Add("静止時間（ms）",_hoverDelay,row++);
        Add("画像文字への対応",_ocrFallback,row++);
        Add("読み上げ速度 (-10～10)",_rate,row++);
        Add("読み上げ音量",_volume,row++);
        Add("読み上げ音声",_voice,row++);
        Add("拡大倍率（現在は無効）",_zoom,row++);
        Add("設定",_triple,row++);
        Add("OCR",_japanese,row++);
        Add("OCR",_copy,row++);
        var buttons=new FlowLayoutPanel {AutoSize=true,WrapContents=true,Dock=DockStyle.Fill};
        AddButton(buttons,"設定を保存",()=>SaveControls());
        AddButton(buttons,"範囲読み上げ",()=>_=ReadSelection());
        AddButton(buttons,"漫画モード",()=>_=ReadBalloon());
        AddButton(buttons,"再読み上げ",()=>_speech.Speak(_lastText,_settings.speech));
        AddButton(buttons,"音声テスト",()=>{
            try {
                DiagnosticLog.Info("音声テスト","設定画面から直接音声再生を試験します。");
                _speech.Speak("これは読み上げ音声のテストです。",_settings.speech);
                Status("音声テストを実行しました。音が聞こえない場合は動作ログを確認してください。");
            } catch(Exception ex){DiagnosticLog.Error("音声テスト",ex);Status("音声テストに失敗しました。動作ログをご確認ください。");}
        });
        AddButton(buttons,"日本語音声を診断",()=>_=DiagnoseJapaneseVoice());
        AddButton(buttons,"停止",()=>_speech.Stop());
        panel.Controls.Add(buttons,0,row); panel.SetColumnSpan(buttons,2);row++;
        var io=new FlowLayoutPanel { AutoSize=true,WrapContents=true,Dock=DockStyle.Fill };
        AddButton(io,"設定を書き出す",Export);
        AddButton(io,"設定を取り込む",Import);
        AddButton(io,"通知領域に戻る",HideSettings);
        AddButton(io,"動作ログを開く",OpenDiagnosticLog);
        AddButton(io,"画面表示を初期状態に戻す",ResetDisplayFromUser);
        AddButton(io,"ログをコピー",()=> {
            try {Clipboard.SetText(DiagnosticLog.Snapshot());Status("診断ログの最近の内容をコピーしました。");}
            catch(Exception ex){DiagnosticLog.Error("ログコピー",ex);Status("ログのコピーに失敗しました。");}
        });
        AddButton(io,"終了",Quit);
        panel.Controls.Add(io,0,row);panel.SetColumnSpan(io,2);row++;
        panel.Controls.Add(_status,0,row);panel.SetColumnSpan(_status,2);row++;
        var tabs=new TabControl { Dock=DockStyle.Fill,AccessibleName="認識文字と動作状況" };
        var diagnosticPage=new TabPage("動作状況（リアルタイム）");
        var lastTextPage=new TabPage("最後に認識した文字");
        diagnosticPage.Controls.Add(_diagnosticEvents);
        lastTextPage.Controls.Add(_last);
        tabs.TabPages.Add(diagnosticPage);
        tabs.TabPages.Add(lastTextPage);
        panel.Controls.Add(tabs,0,row);panel.SetColumnSpan(tabs,2);
        tabs.MinimumSize=new Size(0,150);
        _last.Dock=DockStyle.Fill;
        void Add(string label,Control ctl,int r) {
            var lab=new Label {Text=label,AutoSize=true,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,8,4,7)};
            panel.Controls.Add(lab,0,r);panel.Controls.Add(ctl,1,r);
            ctl.Margin=new Padding(0,4,0,8);lab.AccessibleName=label;
        }
        void AddFull(string label,int r) {
            var lab=new Label {Text=label,AutoSize=true,Margin=new Padding(0,0,0,16)};
            panel.Controls.Add(lab,0,r);panel.SetColumnSpan(lab,2);
        }
        static void AddButton(FlowLayoutPanel p,string text,Action cb) {
            var b=new Button {Text=text,AutoSize=true,Margin=new Padding(2,3,6,6),AccessibleName=text};
            b.Click+=(_,_)=>cb();p.Controls.Add(b);
        }
    }
    private void LoadControls()
    {
        _loadingMode=true;
        try {
            if(_readingMode.Items.Count==0) {
                _readingMode.Items.AddRange(new object[]{
                    "カーソルで自動読み上げ（標準）",
                    "漫画の吹き出しOCR（カーソル）",
                    "範囲選択のみ",
                    "自動読み上げ停止"
                });
                _readingMode.SelectedIndexChanged+=(_,_)=> {
                    if(_loadingMode || _readingMode.SelectedIndex<0)return;
                    var codes=new[]{ReadingModes.Cursor,ReadingModes.Manga,ReadingModes.Selection,ReadingModes.Off};
                    SetMode(codes[_readingMode.SelectedIndex]);
                };
            }
            var modes=new[]{ReadingModes.Cursor,ReadingModes.Manga,ReadingModes.Selection,ReadingModes.Off};
            _readingMode.SelectedIndex=Array.IndexOf(modes,_settings.reading.mode);
            _hoverDelay.Value=_settings.reading.dwellMilliseconds;
            _ocrFallback.Checked=_settings.reading.ocrFallback;
            UpdateModeMenu();
        } finally { _loadingMode=false; }
        _rate.Value=_settings.speech.rate;
        _volume.Value=_settings.speech.volume;
        _zoom.Value=(decimal)_settings.magnifier.zoom*10;
        _japanese.Checked=_settings.ocr.preferJapanese;
        _copy.Checked=_settings.ocr.copyToClipboard;
        _triple.Checked=_settings.magnifier.enableTripleClick;
        _voice.Items.Clear();_voice.Items.Add("（日本語を自動選択）");
        foreach(var name in _speech.VoiceNames) _voice.Items.Add(name);
        int idx=_voice.Items.IndexOf(_settings.speech.voiceName);
        _voice.SelectedIndex=idx>=0?idx:0;
    }
    private void OnLogEntryAdded(string line)
    {
        if(IsDisposed || !IsHandleCreated)return;
        try {
            if(InvokeRequired){BeginInvoke(new Action(()=>OnLogEntryAdded(line)));return;}
            if(IsDisposed)return;
            _diagnosticEvents.AppendText(line+Environment.NewLine);
            if(_diagnosticEvents.TextLength>26000)
                _diagnosticEvents.Text=_diagnosticEvents.Text[^16000..];
            if(Visible){
                _diagnosticEvents.SelectionStart=_diagnosticEvents.TextLength;
                _diagnosticEvents.ScrollToCaret();
            }
        } catch(InvalidOperationException) { /* Form is closing. */ }
    }
    private void RefreshHealth()
    {
        if(IsDisposed)return;
        var state=Visible?"設定画面を表示中（カーソル自動読み上げは一時停止）":
            _busy?"範囲選択・漫画読み取りの処理中":
            _hoverBusy?"カーソル位置の文字認識中":
            ReadingModes.IsAuto(_settings.reading.mode)?"カーソル自動読み上げの監視中":"自動読み上げ停止中";
        _health.Text=$"状態: {state} / モード: {ReadingModes.Label(_settings.reading.mode)} / Ctrl+Alt+R: {(_hotkeyRRegistered?"登録済み":"登録できていません")}" +
            (DiagnosticLog.LastWriteError is string error?" / ログ書込失敗: "+error:"");
        if(++_heartbeatCounter%30==0)
            DiagnosticLog.Info("稼働確認",$"アプリ稼働中。状態={state}、範囲選択中={_busy}、OCR処理中={_hoverBusy}、Ctrl+Alt+R登録済み={_hotkeyRRegistered}。");
    }
    private void OpenDiagnosticLog()
    {
        try {
            if(!File.Exists(DiagnosticLog.FilePath)){
                Status("まだログファイルがありません。"+(DiagnosticLog.LastWriteError??""));
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DiagnosticLog.FilePath){
                UseShellExecute=true
            });
        } catch(Exception ex){DiagnosticLog.Error("ログ表示",ex);Status("ログを開けません。設定画面の動作状況を確認してください。");}
    }
    private void SaveControls()
    {
        try {
            _settings.reading.dwellMilliseconds=(int)_hoverDelay.Value;
            _settings.reading.ocrFallback=_ocrFallback.Checked;
            _settings.speech.rate=(int)_rate.Value;
            _settings.speech.volume=(int)_volume.Value;
            _settings.speech.voiceName=_voice.SelectedIndex<=0?"":_voice.SelectedItem?.ToString()??"";
            _settings.magnifier.zoom=(float)_zoom.Value/10f;
            _settings.magnifier.enableTripleClick=false;
            _settings.ocr.copyToClipboard=_copy.Checked;
            _settings.ocr.preferJapanese=_japanese.Checked;
            _settings.Save();
            DiagnosticLog.Info("設定","設定変更を保存しました。");
            Status("設定を保存しました。");
        } catch(Exception ex) {DiagnosticLog.Error("設定保存",ex);Status("設定保存エラー: "+ex.Message); }
    }
    private void UpdateModeMenu()
    {
        foreach(ToolStripMenuItem item in _modeMenu.DropDownItems)
            item.Checked=item.Text==ReadingModes.Label(_settings.reading.mode);
        _tray.Text="読み上げ："+ReadingModes.Label(_settings.reading.mode);
    }
    private void SetMode(string mode)
    {
        _settings.reading.mode=ReadingModes.Normalize(mode);
        _cursorGeneration++;
        _hoverAttempted=false;
        _lastPointer=new(-100000,-100000);
        _lastAutoText="";
        int index=Array.IndexOf(new[]{ReadingModes.Cursor,ReadingModes.Manga,ReadingModes.Selection,ReadingModes.Off},_settings.reading.mode);
        _loadingMode=true;
        try { if(_readingMode.SelectedIndex!=index) _readingMode.SelectedIndex=index; }
        finally { _loadingMode=false; }
        UpdateModeMenu();
        DiagnosticLog.Info("モード",$"読み上げモードを{ReadingModes.Label(mode)}に変更しました。");
        try { _settings.Save(); Status("読み上げモード："+ReadingModes.Label(mode)); }
        catch(Exception ex) { DiagnosticLog.Error("モード保存",ex);Status("モード設定の保存に失敗: "+ex.Message); }
    }
    private void CursorTimerTick(object? sender,EventArgs e)
    {
        if(!TrayReadingPolicy.CanAutomaticallyRead(Visible,_busy,_settings.reading.mode)) return;
        var point=Cursor.Position;
        var now=Environment.TickCount64;
        if(Math.Abs(point.X-_lastPointer.X)>10 || Math.Abs(point.Y-_lastPointer.Y)>10) {
            _lastPointer=point;
            _pointerMovedAt=now;
            _hoverAttempted=false;
            _cursorGeneration++;
            return;
        }
        if(_hoverBusy || _hoverAttempted || now-_pointerMovedAt<_settings.reading.dwellMilliseconds) return;
        _hoverAttempted=true;
        _=ReadCursorAsync(point,_cursorGeneration,_settings.reading.mode);
    }
    private async Task ReadCursorAsync(Point point,int generation,string mode)
    {
        if(_hoverBusy)return;
        _hoverBusy=true;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        DiagnosticLog.Info("カーソル読み取り",$"カーソル静止を検出しました。モード={ReadingModes.Label(mode)}。");
        try {
            string value="";
            if(mode==ReadingModes.Cursor) {
                value=UiAutomationReader.ReadAtPoint(point);
                DiagnosticLog.Info("文字情報",string.IsNullOrWhiteSpace(value)?"UI Automationから文字情報を取得できませんでした。":$"UI Automationから文字情報を取得。文字数={value.Length}。");
                // Legacy accessibility is still exposed by many Windows/browser controls.
                if(string.IsNullOrWhiteSpace(value)) {
                    value=UiAutomationReader.ReadMsaaAtPoint(point);
                    DiagnosticLog.Info("文字情報",string.IsNullOrWhiteSpace(value)?"MSAAからも文字情報を取得できませんでした。":$"MSAAから文字情報を取得。文字数={value.Length}。");
                }
            }
            if(string.IsNullOrWhiteSpace(value) &&
                (mode==ReadingModes.Manga || _settings.reading.ocrFallback)) {
                DiagnosticLog.Info("カーソルOCR","デジタル文字が得られず、画像OCRを開始します。");
                var desktop=ScreenCapture.VirtualBounds;
                var rect=SelectionGeometry.AroundTap(point,desktop,
                    mode==ReadingModes.Manga?650:520,mode==ReadingModes.Manga?490:220);
                using var image=ScreenCapture.Capture(rect);
                if(mode==ReadingModes.Manga) {
                    var local=new Point(point.X-rect.X,point.Y-rect.Y);
                    var estimate=BalloonDetector.Estimate(image,local);
                    using var bubble=image.Clone(estimate,image.PixelFormat);
                    value=await _ocr.RecognizeAsync(bubble,_settings.ocr.preferJapanese);
                    if(string.IsNullOrWhiteSpace(value))
                        value=await _ocr.RecognizeWithRegionFallbackAsync(image,local,_settings.ocr.preferJapanese,true);
                } else value=await _ocr.RecognizeWithRegionFallbackAsync(image,
                    new Point(point.X-rect.X,point.Y-rect.Y),_settings.ocr.preferJapanese);
            }
            var current=Cursor.Position;
            if(IsDisposed || _busy || generation!=_cursorGeneration ||
                Math.Abs(current.X-point.X)>10 || Math.Abs(current.Y-point.Y)>10 ||
                mode!=_settings.reading.mode || !ReadingModes.IsAuto(mode) || Visible) {
                DiagnosticLog.Info("カーソル読み取り","カーソルが移動したため古い認識結果を破棄しました。");
                return;
            }
            value=value.Trim();
            if(value.Length==0) {
                DiagnosticLog.Warn("カーソル読み取り",$"文字を検出できませんでした。所要時間={watch.ElapsedMilliseconds}ms。");
                return;
            }
            if(value==_lastAutoText)return;
            _lastAutoText=value;
            _lastText=value;
            _last.Text=value;
            DiagnosticLog.Info("カーソル読み取り",$"文字を取得しました。文字数={value.Length}、所要時間={watch.ElapsedMilliseconds}ms。");
            _speech.Speak(value,_settings.speech);
            Status($"カーソル位置から{value.Length}文字の音声読み上げを依頼しました。");
        } catch(Exception ex) {DiagnosticLog.Error("カーソル読み取り",ex);if(!IsDisposed) Status("カーソル読み取りエラー: "+ex.Message); }
        finally { _hoverBusy=false; }
    }
    private void ToggleZoom() => ResetDisplayFromUser();

    private void ResetDisplayFromUser()
    {
        DiagnosticLog.Info("画面復旧","ユーザーから画面復旧を要求しました。");
        var result=MagnificationRecovery.ExplicitReset();
        DiagnosticLog.Info("画面復旧",$"状態={result.Status}。{result.Detail}");
        Status(result.Successful?"画面表示を標準に戻しました。":"画面表示の復旧に失敗しました。動作ログをご確認ください。");
    }
    private async Task ReadSelection()
    {
        if(_busy) {
            DiagnosticLog.Info("範囲読み取り","別の読み取り処理中のためショートカットを受け付けませんでした。");
            return;
        }
        _busy=true;
        _cursorGeneration++;
        bool finishSoundPlayed=false;
        // Audible confirmation is deliberately before the overlay and OCR work.
        SelectionCues.PlayStart();
        DiagnosticLog.Info("範囲読み取り","Ctrl+Alt+Rまたはメニューで範囲読み取りを開始しました。");
        try {
            HideSettings();
            // Let Windows restore the previous application before capturing it.
            await Task.Delay(80);
            using var sel=new SelectionForm();
            DiagnosticLog.Info("範囲読み取り","選択オーバーレイを表示します。クリック・ドラッグ・Enterで決定、Escで取消。");
            _lastHoverSpoken="";
            sel.HoverRequested+=(point,version)=>_ = ReadHoverAsync(sel,point,version);
            DialogResult result;
            try {
                result=sel.ShowDialog();
            } finally {
                // The overlay has ended: play the distinct closing cue immediately,
                // BEFORE potentially slow OCR or subsequent speech playback.
                SelectionCues.PlayEnd();
                finishSoundPlayed=true;
            }
            if(result!=DialogResult.OK || sel.ChosenScreenBounds is null) {
                DiagnosticLog.Info("範囲読み取り","選択をキャンセルしました。");
                return;
            }
            DiagnosticLog.Info("範囲読み取り",$"範囲選択完了。幅={sel.ChosenScreenBounds.Value.Width}px、高さ={sel.ChosenScreenBounds.Value.Height}px。");
            _lastRect=sel.ChosenScreenBounds;
            // Click reads the nearest OCR line; drag reads all lines in the selection.
            Point? focus=sel.ChosenTapScreenPoint is Point tap
                ? new Point(tap.X-_lastRect.Value.X,tap.Y-_lastRect.Value.Y) : null;
            await ReadBitmap(sel.CopyChosenBitmap(),focus);
        } catch(Exception ex) {
            DiagnosticLog.Error("範囲読み取り",ex);
            Status("読み取りエラー: "+ex.Message);
        } finally {
            // Also confirm termination when capture/overlay creation fails.
            if(!finishSoundPlayed) SelectionCues.PlayEnd();
            _busy=false;
            DiagnosticLog.Info("範囲読み取り","処理を終了しました。");
        }
    }
    private async Task ReadBalloon()
    {
        if(_busy){DiagnosticLog.Info("漫画OCR","他の読み取り中のため開始しません。");return;}
        _busy=true;
        DiagnosticLog.Info("漫画OCR","漫画モードを開始しました。");
        try {
            HideSettings();
            await Task.Delay(80);
            using var sel=new SelectionForm(balloonMode:true);
            if(sel.ShowDialog()!=DialogResult.OK || sel.ClickScreenPoint is null){DiagnosticLog.Info("漫画OCR","選択をキャンセルしました。");return;}
            var bounds=sel.DesktopBounds;
            using var whole=sel.CopyScreenshot();
            var click=sel.ClickScreenPoint.Value;
            var rel=new Point(click.X-bounds.Left,click.Y-bounds.Top);
            var estimate=BalloonDetector.Estimate(whole,rel);
            _lastRect=new Rectangle(estimate.X+bounds.X,estimate.Y+bounds.Y,estimate.Width,estimate.Height);
            using var first=whole.Clone(estimate,whole.PixelFormat);
            var text=await _ocr.RecognizeAsync(first,_settings.ocr.preferJapanese);
            if(string.IsNullOrWhiteSpace(text)) {
                var localBounds=new Rectangle(Point.Empty,whole.Size);
                var nearby=SelectionGeometry.AroundTap(rel,localBounds,650,490);
                using var crop=whole.Clone(nearby,whole.PixelFormat);
                text=await _ocr.RecognizeWithRegionFallbackAsync(crop,
                    new Point(rel.X-nearby.X,rel.Y-nearby.Y),_settings.ocr.preferJapanese,true);
            }
            if(string.IsNullOrWhiteSpace(text)) {
                DiagnosticLog.Warn("漫画OCR","吹き出しの文字を認識できませんでした。");
                Status("吹き出しの文字を認識できませんでした。");return;
            }
            DiagnosticLog.Info("漫画OCR",$"吹き出しを認識しました。文字数={text.Length}。");
            _lastText=text;_last.Text=text;
            _speech.Speak(text,_settings.speech);
            Status("漫画の吹き出しを読み上げ中。");
        } catch(Exception ex) {DiagnosticLog.Error("漫画OCR",ex);Status("漫画モードエラー: "+ex.Message);} finally {_busy=false;}
    }
    private async Task ReadBitmap(Bitmap image,Point? focus=null)
    {
        using(image) {
            DiagnosticLog.Info("文字認識",$"OCR開始。画像サイズ={image.Width}x{image.Height}。");
            var watch=System.Diagnostics.Stopwatch.StartNew();
            Status("文字認識中…");
            string text=focus is Point p
                ? await _ocr.RecognizeWithRegionFallbackAsync(image,p,_settings.ocr.preferJapanese)
                : await _ocr.RecognizeWithRegionFallbackAsync(image,new Point(image.Width/2,image.Height/2),_settings.ocr.preferJapanese,true);
            if(string.IsNullOrWhiteSpace(text)) {
                DiagnosticLog.Warn("文字認識",$"文字が見つかりませんでした。所要時間={watch.ElapsedMilliseconds}ms。");
                Status("文字を認識できませんでした。別の範囲を試してください。");return;
            }
            DiagnosticLog.Info("文字認識",$"文字認識成功。文字数={text.Length}、所要時間={watch.ElapsedMilliseconds}ms。");
            _lastText=text;_last.Text=text;
            if(_settings.ocr.copyToClipboard) Clipboard.SetText(text);
            _speech.Speak(text,_settings.speech);
            Status($"読み上げ中（{text.Length}文字）。音声が出ない場合はWindowsの音声を確認してください。");
        }
    }
    private async Task ReadHoverAsync(SelectionForm form, Point screenPoint,int version)
    {
        DiagnosticLog.Info("範囲ホバー","選択画面でカーソル静止を検出。画像OCRを開始します。");
        // OCR can run longer than the dwell interval; serialize requests and ignore stale positions.
        try {
            await _hoverGate.WaitAsync();
            try {
                if(form.IsDisposed || !form.Visible || form.HoverVersion!=version) return;
                var local=new Point(screenPoint.X-form.DesktopBounds.X,screenPoint.Y-form.DesktopBounds.Y);
                var region=SelectionGeometry.AroundTap(local,
                    new Rectangle(Point.Empty,form.DesktopBounds.Size),440,180);
                using var crop=form.CopyLocalRectangle(region);
                var text=await _ocr.RecognizeWithRegionFallbackAsync(crop,
                    new Point(local.X-region.X,local.Y-region.Y),_settings.ocr.preferJapanese);
                if(form.IsDisposed || !form.Visible || form.HoverVersion!=version) return;
                if(string.IsNullOrWhiteSpace(text)){DiagnosticLog.Warn("範囲ホバー","文字を認識できませんでした。");return;}
                if(text==_lastHoverSpoken)return;
                DiagnosticLog.Info("範囲ホバー",$"文字を認識しました。文字数={text.Length}。");
                _lastHoverSpoken=text;
                _lastText=text; _last.Text=text;
                // Hover does not alter the clipboard, even if auto-copy is enabled.
                _speech.Speak(text,_settings.speech);
                Status("カーソル位置を読み上げ中: "+text);
            } finally { _hoverGate.Release(); }
        } catch(Exception ex) {DiagnosticLog.Error("範囲ホバー",ex);if(!form.IsDisposed && form.Visible) Status("カーソル読み取りエラー: "+ex.Message); }
    }
    private async Task DiagnoseJapaneseVoice()
    {
        if(_diagnosingJapanese) return;
        _diagnosingJapanese=true;
        try {
            Status("インストール済み日本語音声を確認中…");
            var result=await Task.Run(JapaneseSpeechDiagnostic.Run);
            if(IsDisposed) return;
            _last.Text=result.Report;
            Status(result.HasJapaneseVoice
                ? "日本語音声の速度測定が終了しました。診断結果を保存しました。"
                : "このWindowsではSAPI対応の日本語音声が見つかりませんでした。診断結果を保存しました。");
            // Don't automatically speak the report: diagnostics must be non-intrusive.
        } catch(Exception ex) {
            if(!IsDisposed) Status("日本語音声診断エラー: "+ex.Message);
        } finally { _diagnosingJapanese=false; }
    }
    private void ReadFocused()
    {
        try {
            string text=UiAutomationReader.ReadFocused();
            if(string.IsNullOrWhiteSpace(text)) {Status("フォーカス中のUI要素から文字を取得できませんでした。範囲読み上げを試してください。");return;}
            _lastText=text;_last.Text=text;
            _speech.Speak(text,_settings.speech);Status("UI Automationから読み上げました。");
        } catch(Exception ex){Status("UI Automationエラー: "+ex.Message);}
    }
    private void Export()
    {
        SaveControls();
        using var dialog=new SaveFileDialog{Filter="INI ファイル|*.ini",FileName="settings.ini"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            try { File.WriteAllText(dialog.FileName,PortableIni.Write(_settings),new System.Text.UTF8Encoding(false));Status("設定を書き出しました。"); }
            catch(Exception ex){Status("書き出し失敗: "+ex.Message);}
        }
    }
    private void Import()
    {
        using var dialog=new OpenFileDialog{Filter="INI 設定または旧JSON|*.ini;*.json|INI ファイル|*.ini|旧JSON ファイル|*.json"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            try {
                if(new FileInfo(dialog.FileName).Length>100_000) throw new InvalidDataException("設定ファイルが大きすぎます。");
                _settings=Path.GetExtension(dialog.FileName).Equals(".ini",StringComparison.OrdinalIgnoreCase)
                    ? PortableIni.Parse(File.ReadAllText(dialog.FileName,System.Text.Encoding.UTF8))
                    : AppSettings.Normalize(JsonSerializer.Deserialize<AppSettings>(
                        File.ReadAllText(dialog.FileName),AppSettings.JsonOptions)??new());
                _settings.Save();LoadControls();Status("設定を取り込みました。");
            } catch(Exception ex){Status("取り込み失敗: "+ex.Message);}
        }
    }
    private void Status(string text) { if(IsDisposed)return; if(InvokeRequired) {BeginInvoke(new Action(()=>Status(text)));return;} _status.Text=text; }
    private void HideSettings()
    {
        if(Visible)DiagnosticLog.Info("設定画面","設定画面を隠しました。自動読み上げを再開できます。");
        Hide();
        _settingsMayBeShown=false;
    }
    private void OpenSettings()
    {
        DiagnosticLog.Info("設定画面","設定画面を開き、リアルタイム診断を表示します。");
        _settingsMayBeShown=true;
        if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal;
        Show();
        WindowState=FormWindowState.Normal;
        BringToFront();
        Activate();
    }
    private void Quit() {DiagnosticLog.Info("終了","通知領域などから終了しました。");_quitting=true;Close();}
    protected override void Dispose(bool disposing)
    {
        if(disposing){DiagnosticLog.EntryAdded-=OnLogEntryAdded;_healthTimer.Stop();_healthTimer.Dispose();_cursorTimer.Stop();_cursorTimer.Dispose();_hoverGate.Dispose();_speech.Dispose();_tray.Visible=false;_tray.Dispose();}
        base.Dispose(disposing);
    }
}
