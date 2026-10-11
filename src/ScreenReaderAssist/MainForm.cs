using System.Drawing;
using System.Text.Json;
namespace ScreenReaderAssist;

public sealed class MainForm : Form
{
    private AppSettings _settings;
    private readonly SpeechService _speech = new();
    private readonly OcrService _ocr = new();
    private readonly MagnificationService _magnifier = new();
    private MouseTripleClickHook? _mouseHook;
    private readonly NotifyIcon _tray;
    private readonly NumericUpDown _rate = new() { Minimum=-10, Maximum=10, Width=80 };
    private readonly NumericUpDown _volume = new() { Minimum=0, Maximum=100, Width=80 };
    private readonly NumericUpDown _zoom = new() { Minimum=15, Maximum=60, Increment=5, Width=80 };
    private readonly ComboBox _voice = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=330 };
    private readonly CheckBox _triple = new() { Text="3回クリックの一時拡大を有効にする（試験的）", AutoSize=true };
    private readonly CheckBox _copy = new() { Text="認識した文字をクリップボードへコピー", AutoSize=true };
    private readonly CheckBox _japanese = new() { Text="日本語OCRを優先", AutoSize=true };
    private readonly Label _status = new() { Text="準備完了", AutoSize=false, Width=540, Height=60, AccessibleName="現在の状態", AutoEllipsis=true };
    private readonly TextBox _last = new() { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, Height=110, Dock=DockStyle.Fill, AccessibleName="最後に認識した文章" };
    private Rectangle? _lastRect;
    private bool _busy,_quitting;
    private string _lastText="";

    public MainForm()
    {
        try {_settings=AppSettings.Load();} catch { _settings=new AppSettings(); }
        Text="Screen Reader Assist 設定";
        AccessibleName="Screen Reader Assist 設定画面";
        MinimumSize=new Size(660,610);
        Size=new Size(720,660);
        StartPosition=FormStartPosition.CenterScreen;
        FormClosing+=(_,e)=>{ if(!_quitting) {e.Cancel=true;Hide();} };
        _tray=new NotifyIcon { Icon=SystemIcons.Information, Visible=true, Text="Screen Reader Assist" };
        var menu=new ContextMenuStrip();
        menu.Items.Add("範囲を読み上げ",null,async (_,_)=>await ReadSelection());
        menu.Items.Add("漫画の吹き出しを読む",null,async (_,_)=>await ReadBalloon());
        menu.Items.Add("注目中のUI要素を読む",null,(_,_)=>ReadFocused());
        menu.Items.Add("読み上げ停止",null,(_,_)=>_speech.Stop());
        menu.Items.Add("設定を開く",null,(_,_)=>OpenSettings());
        menu.Items.Add("終了",null,(_,_)=>Quit());
        _tray.ContextMenuStrip=menu;
        _tray.DoubleClick+=(_,_)=>OpenSettings();
        BuildForm();
        LoadControls();
        Shown+=(_,_)=>BeginInvoke(new Action(Hide));
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Register(1,Keys.R); Register(2,Keys.M); Register(3,Keys.S); Register(4,Keys.U); Register(5,Keys.P); Register(6,Keys.Z);
    }
    private void Register(int id,Keys k)
    {
        if(!NativeHotkeys.RegisterHotKey(Handle,id,NativeHotkeys.Alt | NativeHotkeys.Shift,(int)k))
            _status.Text=$"ショートカット Alt+Shift+{k} が競合しています。";
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        for(int i=1;i<=6;i++) NativeHotkeys.UnregisterHotKey(Handle,i);
        base.OnHandleDestroyed(e);
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==NativeHotkeys.WmHotkey) {
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
        var panel=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),AutoScroll=true,ColumnCount=2,RowCount=13 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,185));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<12;i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Controls.Add(panel);
        int row=0;
        AddFull("Alt+Shift+R：範囲読み上げ　M：漫画　U：UI要素　S：停止　P：一時停止　Z：拡大",row++);
        Add("読み上げ速度 (-10～10)",_rate,row++);
        Add("読み上げ音量",_volume,row++);
        Add("読み上げ音声",_voice,row++);
        Add("拡大倍率 (×0.1)",_zoom,row++);
        Add("設定",_triple,row++);
        Add("OCR",_japanese,row++);
        Add("OCR",_copy,row++);
        var buttons=new FlowLayoutPanel {AutoSize=true,WrapContents=true,Dock=DockStyle.Fill};
        AddButton(buttons,"設定を保存",()=>SaveControls());
        AddButton(buttons,"範囲読み上げ",()=>_=ReadSelection());
        AddButton(buttons,"漫画モード",()=>_=ReadBalloon());
        AddButton(buttons,"再読み上げ",()=>_speech.Speak(_lastText,_settings.speech));
        AddButton(buttons,"停止",()=>_speech.Stop());
        panel.Controls.Add(buttons,0,row); panel.SetColumnSpan(buttons,2);row++;
        var io=new FlowLayoutPanel { AutoSize=true,WrapContents=true,Dock=DockStyle.Fill };
        AddButton(io,"設定を書き出す",Export);
        AddButton(io,"設定を取り込む",Import);
        AddButton(io,"通知領域に戻る",Hide);
        AddButton(io,"終了",Quit);
        panel.Controls.Add(io,0,row);panel.SetColumnSpan(io,2);row++;
        panel.Controls.Add(_status,0,row);panel.SetColumnSpan(_status,2);row++;
        panel.Controls.Add(_last,0,row);panel.SetColumnSpan(_last,2);
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
        TryUpdateHook();
    }
    private void SaveControls()
    {
        try {
            _settings.speech.rate=(int)_rate.Value;
            _settings.speech.volume=(int)_volume.Value;
            _settings.speech.voiceName=_voice.SelectedIndex<=0?"":_voice.SelectedItem?.ToString()??"";
            _settings.magnifier.zoom=(float)_zoom.Value/10f;
            _settings.magnifier.enableTripleClick=_triple.Checked;
            _settings.ocr.copyToClipboard=_copy.Checked;
            _settings.ocr.preferJapanese=_japanese.Checked;
            _settings.Save();TryUpdateHook();Status("設定を保存しました。");
        } catch(Exception ex) { Status("設定保存エラー: "+ex.Message); }
    }
    private void TryUpdateHook()
    {
        if(!_settings.magnifier.enableTripleClick) { _mouseHook?.Dispose();_mouseHook=null;return; }
        if(_mouseHook is not null)return;
        try {
            _mouseHook=new MouseTripleClickHook();
            _mouseHook.OnTripleDown=p=>_magnifier.Start(p,_settings.magnifier.zoom);
            _mouseHook.OnDrag=p=>_magnifier.Drag(p);
            _mouseHook.OnRelease=()=>_magnifier.Restore();
        } catch(Exception ex) { _settings.magnifier.enableTripleClick=false;_triple.Checked=false;Status("3回クリック検出を開始できません: "+ex.Message); }
    }
    private void ToggleZoom()
    {
        if(_magnifier.Active) _magnifier.Restore();
        else if(!_magnifier.Start(Cursor.Position,_settings.magnifier.zoom)) Status("拡大を開始できません。Windowsの拡大鏡との競合等を確認してください。");
    }
    private async Task ReadSelection()
    {
        if(_busy)return;
        _busy=true;
        try {
            Hide();
            using var sel=new SelectionForm();
            if(sel.ShowDialog()!=DialogResult.OK || sel.ChosenScreenBounds is null) return;
            _lastRect=sel.ChosenScreenBounds;
            await CaptureAndRead(_lastRect.Value);
        } catch(Exception ex) {Status("読み取りエラー: "+ex.Message);} finally {_busy=false;}
    }
    private async Task ReadBalloon()
    {
        if(_busy)return;
        _busy=true;
        try {
            Hide();
            using var sel=new SelectionForm(balloonMode:true);
            if(sel.ShowDialog()!=DialogResult.OK || sel.ClickScreenPoint is null)return;
            await Task.Delay(150);
            var bounds=ScreenCapture.VirtualBounds;
            using var whole=ScreenCapture.Capture(bounds);
            var click=sel.ClickScreenPoint.Value;
            var rel=new Point(click.X-bounds.Left,click.Y-bounds.Top);
            var estimate=BalloonDetector.Estimate(whole,rel);
            _lastRect=new Rectangle(estimate.X+bounds.X,estimate.Y+bounds.Y,estimate.Width,estimate.Height);
            await ReadBitmap(whole.Clone(estimate,whole.PixelFormat));
        } catch(Exception ex) { Status("漫画モードエラー: "+ex.Message);} finally {_busy=false;}
    }
    private async Task CaptureAndRead(Rectangle rect)
    {
        // Let the overlay close before taking a new screenshot.
        await Task.Delay(150);
        using var bmp=ScreenCapture.Capture(rect);
        await ReadBitmap((Bitmap)bmp.Clone());
    }
    private async Task ReadBitmap(Bitmap image)
    {
        using(image) {
            Status("文字認識中…");
            string text=await _ocr.RecognizeAsync(image,_settings.ocr.preferJapanese);
            if(string.IsNullOrWhiteSpace(text)) {Status("文字を認識できませんでした。別の範囲を試してください。");return;}
            _lastText=text;_last.Text=text;
            if(_settings.ocr.copyToClipboard) Clipboard.SetText(text);
            _speech.Speak(text,_settings.speech);
            Status($"読み上げ中（{text.Length}文字）。音声が出ない場合はWindowsの音声を確認してください。");
        }
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
        using var dialog=new SaveFileDialog{Filter="JSON ファイル|*.json",FileName="screen-reader-assist-settings.json"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            try { File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(_settings,AppSettings.JsonOptions));Status("設定を書き出しました。"); }
            catch(Exception ex){Status("書き出し失敗: "+ex.Message);}
        }
    }
    private void Import()
    {
        using var dialog=new OpenFileDialog{Filter="JSON ファイル|*.json"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            try {
                if(new FileInfo(dialog.FileName).Length>100_000) throw new InvalidDataException("設定ファイルが大きすぎます。");
                _settings=AppSettings.Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(dialog.FileName),AppSettings.JsonOptions)??new());
                _settings.Save();LoadControls();Status("設定を取り込みました。");
            } catch(Exception ex){Status("取り込み失敗: "+ex.Message);}
        }
    }
    private void Status(string text) { if(IsDisposed)return; if(InvokeRequired) {BeginInvoke(new Action(()=>Status(text)));return;} _status.Text=text; }
    private void OpenSettings() {Show();WindowState=FormWindowState.Normal;Activate();}
    private void Quit() {_quitting=true;Close();}
    protected override void Dispose(bool disposing)
    {
        if(disposing){_mouseHook?.Dispose();_magnifier.Dispose();_speech.Dispose();_tray.Visible=false;_tray.Dispose();}
        base.Dispose(disposing);
    }
}
