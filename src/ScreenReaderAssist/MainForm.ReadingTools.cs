using System.Drawing;

namespace ScreenReaderAssist;

public sealed partial class MainForm
{
    private Form? _historyWindow;
    private Control BuildReadingHistoryPanel()
    {
        var panel = new TableLayoutPanel {
            Dock=DockStyle.Fill,Padding=new Padding(8),ColumnCount=1,RowCount=3
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var searchLine = new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
        searchLine.Controls.Add(new Label {
            Text="最新30件・終了すると消去（ディスクに保存しません）",
            AutoSize=true,Font=new Font("Meiryo",11f,FontStyle.Bold)
        });
        searchLine.Controls.Add(_historySearch);
        _historySearch.TextChanged+=(_,_)=>RefreshReadingHistory();
        panel.Controls.Add(searchLine,0,0);
        _historyList.Font=new Font("Meiryo",12f,FontStyle.Bold);
        _historyList.BackColor=Color.FromArgb(13,20,31);
        _historyList.ForeColor=Color.White;
        _historyList.SelectedIndexChanged+=(_,_)=>{
            if(_historyList.SelectedItem is ReadingEntry entry) {
                _readerSelectedText=entry.Text;
                if(_readerWindow is {IsDisposed:false} && _readerContent!=null)
                    _readerContent.Text=entry.Text;
            }
        };
        _historyList.DoubleClick+=(_,_)=>OpenReadingWindow(true);
        panel.Controls.Add(_historyList,0,1);
        var actions = new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
        AddReadingAction(actions,"履歴を大画面で開く",OpenReadingHistoryWindow);
        AddReadingAction(actions,"大きな文字で読む",()=>OpenReadingWindow(true));
        AddReadingAction(actions,"選択した文章を読み上げ",()=>{
            if(_historyList.SelectedItem is ReadingEntry entry) {
                _speech.Speak(entry.Text,_settings.speech);
                Status("履歴の文章を読み上げています。");
            }
        });
        AddReadingAction(actions,"選択文をコピー",()=>{
            if(_historyList.SelectedItem is ReadingEntry entry) {
                try {Clipboard.SetText(entry.Text);Status("選択文をコピーしました。");}
                catch(Exception ex){DiagnosticLog.Error("履歴コピー",ex);Status("コピーできませんでした。");}
            }
        });
        AddReadingAction(actions,"クリップボードを読む",ReadClipboardOnDemand);
        AddReadingAction(actions,"履歴をすべて消す",ClearReadingHistory);
        panel.Controls.Add(actions,0,2);
        return panel;
    }

    private void ClearReadingHistory()
    {
        _readingShelf.Clear();
        _readerSelectedText="";
        _lastText="";
        _last.Clear();
        if(_readerContent!=null && !_readerContent.IsDisposed)_readerContent.Clear();
        RefreshReadingHistory();
        Status("読み上げ履歴と最後の読み取り内容を消しました。");
    }

    private static void AddReadingAction(FlowLayoutPanel panel,string title,Action action)
    {
        var button=new Button {
            Text=title,AutoSize=true,MinimumSize=new Size(145,48),
            AccessibleName=title,Margin=new Padding(4),
            Font=new Font("Meiryo",11f,FontStyle.Bold),
            FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false,
            BackColor=Color.FromArgb(42,64,91),ForeColor=Color.White
        };
        button.FlatAppearance.BorderSize=3;
        button.FlatAppearance.BorderColor=Color.FromArgb(255,224,47);
        button.FlatAppearance.MouseDownBackColor=Color.FromArgb(255,224,47);
        button.MouseDown+=(_,e)=>{
            if(e.Button==MouseButtons.Left){
                button.BackColor=Color.FromArgb(255,224,47);
                button.ForeColor=Color.Black;
            }
        };
        button.MouseUp+=(_,_)=>{
            button.BackColor=Color.FromArgb(42,64,91);
            button.ForeColor=Color.White;
        };
        button.MouseLeave+=(_,_)=>{
            button.BackColor=Color.FromArgb(42,64,91);
            button.ForeColor=Color.White;
        };
        button.Click+=(_,_)=>action();
        panel.Controls.Add(button);
    }

    private void RecordRead(string source,string text)
    {
        if(!_readingShelf.Add(source,text))return;
        if(IsDisposed)return;
        RefreshReadingHistory();
    }

    private void RefreshReadingHistory()
    {
        if(_historyList.IsDisposed)return;
        var previous=_historyList.SelectedItem as ReadingEntry;
        var entries=_readingShelf.Search(_historySearch.Text);
        _historyList.BeginUpdate();
        try {
            _historyList.Items.Clear();
            foreach(var item in entries)_historyList.Items.Add(item);
            if(previous!=null && entries.Contains(previous))
                _historyList.SelectedItem=previous;
            else if(_historyList.Items.Count>0)
                _historyList.SelectedIndex=0;
        }
        finally {_historyList.EndUpdate();}
    }

    private void OpenReadingHistoryWindow()
    {
        if(_historyWindow is {IsDisposed:false}) {
            _historyWindow.Activate();return;
        }
        var panel=_historyList.Parent;
        if(panel==null || panel.Parent==null) return;
        var originalParent=panel.Parent;
        var window=new Form {
            Text="読み上げ履歴（最新30件・一時保存）",
            Size=new Size(1050,730),MinimumSize=new Size(620,440),
            StartPosition=FormStartPosition.CenterScreen,
            Font=new Font("Meiryo",12f,FontStyle.Bold),
            BackColor=Color.FromArgb(17,23,35),ForeColor=Color.White,
            AccessibleName="読み上げ履歴を大きく表示するウィンドウ"
        };
        _historyWindow=window;
        originalParent.Controls.Remove(panel);
        window.Controls.Add(panel);
        panel.Dock=DockStyle.Fill;
        window.FormClosed+=(_,_)=>{
            if(!IsDisposed && !originalParent.IsDisposed && !panel.IsDisposed) {
                window.Controls.Remove(panel);
                originalParent.Controls.Add(panel);
                panel.Dock=DockStyle.Fill;
            }
            _historyWindow=null;
        };
        window.Show();
        window.Activate();
    }

    private void ReadClipboardOnDemand()
    {
        try {
            if(!Clipboard.ContainsText()){
                Status("クリップボードに文字がありません。");return;
            }
            string text=Clipboard.GetText();
            if(string.IsNullOrWhiteSpace(text)){
                Status("クリップボードに文字がありません。");return;
            }
            // Explicitly initiated; never overwrite the clipboard or auto-monitor it.
            string safe=ReadingShelf.TruncateWithoutBreakingSurrogate(
                text,ReadingShelf.MaximumCharacters);
            _lastText=safe;
            _last.Text=safe;
            RecordRead("クリップボード",safe);
            _speech.Speak(safe,_settings.speech);
            Status("クリップボードの文章を読み上げ中。");
        }
        catch(Exception ex){
            DiagnosticLog.Error("クリップボード読み上げ",ex);
            Status("クリップボードを読み取れませんでした。");
        }
    }

    private void OpenReadingWindow(bool fromHistory=false)
    {
        if(fromHistory && _historyList.SelectedItem is ReadingEntry entry)
            _readerSelectedText=entry.Text;
        else if(!fromHistory && !string.IsNullOrWhiteSpace(_lastText))
            _readerSelectedText=_lastText;
        if(string.IsNullOrWhiteSpace(_readerSelectedText))
            _readerSelectedText=_lastText;
        if(string.IsNullOrWhiteSpace(_readerSelectedText)){
            Status("先に文章を読み取るか、クリップボードの文章を読み上げてください。");
            return;
        }
        if(_readerWindow is {IsDisposed:false}){
            if(_readerContent!=null)_readerContent.Text=_readerSelectedText;
            _readerWindow.Show();
            _readerWindow.Activate();
            return;
        }
        var reader=new Form {
            Text="大きな文字の読書ビュー",
            Size=new Size(1000,720),MinimumSize=new Size(520,360),
            StartPosition=FormStartPosition.CenterScreen,
            BackColor=Color.Black,ForeColor=Color.White,
            Font=new Font("Meiryo",13f,FontStyle.Bold),
            AccessibleName="大きな文字で読み取った文章を読む画面"
        };
        _readerWindow=reader;
        var layout=new TableLayoutPanel {
            Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(10)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var buttons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};
        AddReadingAction(buttons,"文字＋",()=>{
            _readerFontSize=Math.Min(60f,_readerFontSize+4f);ApplyReaderStyle();
        });
        AddReadingAction(buttons,"文字－",()=>{
            _readerFontSize=Math.Max(16f,_readerFontSize-4f);ApplyReaderStyle();
        });
        AddReadingAction(buttons,"白黒反転",()=>{
            _readerReverse=!_readerReverse;ApplyReaderStyle();
        });
        AddReadingAction(buttons,"再読み上げ",()=>{
            if(_readerContent!=null)_speech.Speak(_readerContent.Text,_settings.speech);
        });
        AddReadingAction(buttons,"音声停止",()=>_speech.Stop());
        AddReadingAction(buttons,"選択部分をコピー",()=>{
            if(_readerContent==null || string.IsNullOrEmpty(_readerContent.SelectedText)){
                Status("読書ビューで文字を選択してください。");return;
            }
            try {Clipboard.SetText(_readerContent.SelectedText);}
            catch(Exception ex){DiagnosticLog.Error("読書ビューコピー",ex);Status("コピーできませんでした。");}
        });
        AddReadingAction(buttons,"閉じる",()=>reader.Close());
        layout.Controls.Add(buttons,0,0);
        _readerContent=new RichTextBox {
            Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,
            WordWrap=true,ScrollBars=RichTextBoxScrollBars.Vertical,
            BorderStyle=BorderStyle.FixedSingle,
            AccessibleName="読み取った文章。大きな太字で表示。範囲を選択してコピーできます",
            Text=_readerSelectedText
        };
        layout.Controls.Add(_readerContent,0,1);
        reader.Controls.Add(layout);
        reader.FormClosed+=(_,_)=>{
            _readerWindow=null;_readerContent=null;
        };
        ApplyReaderStyle();
        reader.Show(); // Standalone reader also works while main settings stay hidden.
        reader.Activate();
    }

    private void ApplyReaderStyle()
    {
        if(_readerContent==null || _readerContent.IsDisposed)return;
        _readerContent.BackColor=_readerReverse?Color.Black:Color.White;
        _readerContent.ForeColor=_readerReverse?Color.White:Color.Black;
        _readerContent.Font=new Font("Meiryo",_readerFontSize,FontStyle.Bold);
    }
}
