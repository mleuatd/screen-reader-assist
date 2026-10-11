using System.Drawing;
using System.Text.Json;
namespace ScreenReaderAssist;

public sealed class MainForm : Form
{
    private AppSettings _settings;
    private readonly SpeechService _speech = new();
    private readonly OcrService _ocr = new();
    private readonly MagnificationService _magnifier = new();
    private readonly NotifyIcon _tray;
    private readonly NumericUpDown _rate = new() { Minimum=-10, Maximum=10, Width=80 };
    private readonly NumericUpDown _volume = new() { Minimum=0, Maximum=100, Width=80 };
    private readonly NumericUpDown _zoom = new() { Minimum=15, Maximum=60, Increment=5, Width=80 };
    private readonly ComboBox _voice = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=330 };
    private readonly CheckBox _triple = new() { Text="3回クリック拡大は無効（競合防止）。拡大は Alt+Shift+Z", AutoSize=true, Enabled=false };
    private readonly CheckBox _copy = new() { Text="認識した文字をクリップボードへコピー", AutoSize=true };
    private readonly CheckBox _japanese = new() { Text="日本語OCRを優先", AutoSize=true };
    private readonly Label _status = new() { Text="準備完了", AutoSize=false, Width=540, Height=60, AccessibleName="現在の状態", AutoEllipsis=true };
    private readonly TextBox _last = new() { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, Height=110, Dock=DockStyle.Fill, AccessibleName="最後に認識した文章" };
    private Rectangle? _lastRect;
    private bool _busy,_quitting;
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
        foreach(var code in new[]{ReadingModes.Cursor,ReadingModes.Manga,ReadingModes.Selection,ReadingModes.Off}) {
            var selected=code;
            _modeMenu.DropDownItems.Add(ReadingModes.Label(code),null,(_,_)=>SetMode(selected));
        }
        menu.Items.Add(_modeMenu);
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
        _cursorTimer.Tick+=CursorTimerTick;
        _pointerMovedAt=Environment.TickCount64;
        _cursorTimer.Start();
        Shown+=(_,_)=>BeginInvoke(new Action(Hide));
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Register(1,Keys.R); Register(2,Keys.M); Register(3,Keys.S); Register(4,Keys.U); Register(5,Keys.P); Register(6,Keys.Z);
    }
    private void Register(int id,Keys k)
    {
        var modifiers = k==Keys.R ? NativeHotkeys.Control | NativeHotkeys.Alt : NativeHotkeys.Alt | NativeHotkeys.Shift;
        if(!NativeHotkeys.RegisterHotKey(Handle,id,modifiers | NativeHotkeys.NoRepeat,(int)k))
            _status.Text=$"ショートカット {(k==Keys.R ? "Ctrl+Alt+R" : $"Alt+Shift+{k}")} が競合しています。";
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
        var panel=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),AutoScroll=true,ColumnCount=2,RowCount=16 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,185));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<15;i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Controls.Add(panel);
        int row=0;
        AddFull("Ctrl+Alt+R：範囲読み上げ（タッチ／ドラッグ）。Alt+Shift+M：漫画、U：UI、S：停止、P：一時停止、Z：拡大",row++);
        Add("読み上げモード",_readingMode,row++);
        Add("静止時間（ms）",_hoverDelay,row++);
        Add("画像文字への対応",_ocrFallback,row++);
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
            _settings.Save();Status("設定を保存しました。");
        } catch(Exception ex) { Status("設定保存エラー: "+ex.Message); }
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
        try { _settings.Save(); Status("読み上げモード："+ReadingModes.Label(mode)); }
        catch(Exception ex) { Status("モード設定の保存に失敗: "+ex.Message); }
    }
    private void CursorTimerTick(object? sender,EventArgs e)
    {
        if(_busy || Visible || !ReadingModes.IsAuto(_settings.reading.mode)) return;
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
        try {
            string value="";
            if(mode==ReadingModes.Cursor) {
                value=UiAutomationReader.ReadAtPoint(point);
                // Legacy accessibility is still exposed by many Windows/browser controls.
                if(string.IsNullOrWhiteSpace(value))
                    value=UiAutomationReader.ReadMsaaAtPoint(point);
            }
            if(string.IsNullOrWhiteSpace(value) &&
                (mode==ReadingModes.Manga || _settings.reading.ocrFallback)) {
                var desktop=ScreenCapture.VirtualBounds;
                var rect=SelectionGeometry.AroundTap(point,desktop,
                    mode==ReadingModes.Manga?650:520,mode==ReadingModes.Manga?490:220);
                using var image=ScreenCapture.Capture(rect);
                if(mode==ReadingModes.Manga) {
                    var local=new Point(point.X-rect.X,point.Y-rect.Y);
                    var estimate=BalloonDetector.Estimate(image,local);
                    using var bubble=image.Clone(estimate,image.PixelFormat);
                    value=await _ocr.RecognizeAsync(bubble,_settings.ocr.preferJapanese);
                } else value=await _ocr.RecognizeNearPointAsync(image,
                    new Point(point.X-rect.X,point.Y-rect.Y),_settings.ocr.preferJapanese);
            }
            var current=Cursor.Position;
            if(IsDisposed || _busy || generation!=_cursorGeneration ||
                Math.Abs(current.X-point.X)>10 || Math.Abs(current.Y-point.Y)>10 ||
                mode!=_settings.reading.mode || !ReadingModes.IsAuto(mode) || Visible) return;
            value=value.Trim();
            if(value.Length==0 || value==_lastAutoText) return;
            _lastAutoText=value;
            _lastText=value;
            _last.Text=value;
            _speech.Speak(value,_settings.speech);
            Status("カーソルの文字を読み上げ: "+value);
        } catch(Exception ex) { if(!IsDisposed) Status("カーソル読み取りエラー: "+ex.Message); }
        finally { _hoverBusy=false; }
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
        _cursorGeneration++;
        try {
            Hide();
            // Let Windows restore the previous application before capturing it.
            await Task.Delay(80);
            using var sel=new SelectionForm();
            _lastHoverSpoken="";
            sel.HoverRequested+=(point,version)=>_ = ReadHoverAsync(sel,point,version);
            if(sel.ShowDialog()!=DialogResult.OK || sel.ChosenScreenBounds is null) return;
            _lastRect=sel.ChosenScreenBounds;
            // Click reads the nearest OCR line; drag reads all lines in the selection.
            Point? focus=sel.ChosenTapScreenPoint is Point tap
                ? new Point(tap.X-_lastRect.Value.X,tap.Y-_lastRect.Value.Y) : null;
            await ReadBitmap(sel.CopyChosenBitmap(),focus);
        } catch(Exception ex) {Status("読み取りエラー: "+ex.Message);} finally {_busy=false;}
    }
    private async Task ReadBalloon()
    {
        if(_busy)return;
        _busy=true;
        try {
            Hide();
            await Task.Delay(80);
            using var sel=new SelectionForm(balloonMode:true);
            if(sel.ShowDialog()!=DialogResult.OK || sel.ClickScreenPoint is null)return;
            var bounds=sel.DesktopBounds;
            using var whole=sel.CopyScreenshot();
            var click=sel.ClickScreenPoint.Value;
            var rel=new Point(click.X-bounds.Left,click.Y-bounds.Top);
            var estimate=BalloonDetector.Estimate(whole,rel);
            _lastRect=new Rectangle(estimate.X+bounds.X,estimate.Y+bounds.Y,estimate.Width,estimate.Height);
            await ReadBitmap(whole.Clone(estimate,whole.PixelFormat));
        } catch(Exception ex) { Status("漫画モードエラー: "+ex.Message);} finally {_busy=false;}
    }
    private async Task ReadBitmap(Bitmap image,Point? focus=null)
    {
        using(image) {
            Status("文字認識中…");
            string text=focus is Point p
                ? await _ocr.RecognizeNearPointAsync(image,p,_settings.ocr.preferJapanese)
                : await _ocr.RecognizeAsync(image,_settings.ocr.preferJapanese);
            if(string.IsNullOrWhiteSpace(text)) {Status("文字を認識できませんでした。別の範囲を試してください。");return;}
            _lastText=text;_last.Text=text;
            if(_settings.ocr.copyToClipboard) Clipboard.SetText(text);
            _speech.Speak(text,_settings.speech);
            Status($"読み上げ中（{text.Length}文字）。音声が出ない場合はWindowsの音声を確認してください。");
        }
    }
    private async Task ReadHoverAsync(SelectionForm form, Point screenPoint,int version)
    {
        // OCR can run longer than the dwell interval; serialize requests and ignore stale positions.
        try {
            await _hoverGate.WaitAsync();
            try {
                if(form.IsDisposed || !form.Visible || form.HoverVersion!=version) return;
                var local=new Point(screenPoint.X-form.DesktopBounds.X,screenPoint.Y-form.DesktopBounds.Y);
                var region=SelectionGeometry.AroundTap(local,
                    new Rectangle(Point.Empty,form.DesktopBounds.Size),440,180);
                using var crop=form.CopyLocalRectangle(region);
                var text=await _ocr.RecognizeNearPointAsync(crop,
                    new Point(local.X-region.X,local.Y-region.Y),_settings.ocr.preferJapanese);
                if(form.IsDisposed || !form.Visible || form.HoverVersion!=version) return;
                if(string.IsNullOrWhiteSpace(text) || text==_lastHoverSpoken) return;
                _lastHoverSpoken=text;
                _lastText=text; _last.Text=text;
                // Hover does not alter the clipboard, even if auto-copy is enabled.
                _speech.Speak(text,_settings.speech);
                Status("カーソル位置を読み上げ中: "+text);
            } finally { _hoverGate.Release(); }
        } catch(Exception ex) { if(!form.IsDisposed && form.Visible) Status("カーソル読み取りエラー: "+ex.Message); }
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
        if(disposing){_cursorTimer.Stop();_cursorTimer.Dispose();_hoverGate.Dispose();_magnifier.Dispose();_speech.Dispose();_tray.Visible=false;_tray.Dispose();}
        base.Dispose(disposing);
    }
}
