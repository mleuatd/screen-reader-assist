using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ScreenReaderAssist;

internal static class ReadingUiSmoke
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int _cases;
    private static void Check(bool value,string title)
    {
        if(!value) throw new Exception("FAIL UI: "+title);
        Interlocked.Increment(ref _cases);
        Console.WriteLine("PASS UI: "+title);
    }
    private static object? Get(object form,string field)
    {
        return form.GetType().GetField(field,Private)?.GetValue(form)
            ?? throw new MissingFieldException(field);
    }
    private static void Set(object form,string field,object value)
    {
        var info=form.GetType().GetField(field,Private)
            ?? throw new MissingFieldException(field);
        info.SetValue(form,value);
    }
    private static void Call(object form,string method,params object[] parameters)
    {
        var info=form.GetType().GetMethod(method,Private)
            ?? throw new MissingMethodException(method);
        info.Invoke(form,parameters);
    }
    public static int Run()
    {
        Exception? failure=null;
        using var done=new ManualResetEventSlim(false);
        var thread=new Thread(()=>{
            try {CheckForms();}
            catch(Exception ex){failure=ex;}
            finally{done.Set();}
        });
        thread.Name="Reader UI STA smoke";
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground=true;
        thread.Start();
        if(!done.Wait(TimeSpan.FromSeconds(45))){
            Console.Error.WriteLine("FAIL UI: did not finish within 45 seconds");
            return 1;
        }
        if(failure!=null){
            Console.Error.WriteLine(failure);
            return 1;
        }
        Console.WriteLine("PASS UI: "+_cases+" cases completed on Windows STA");
        return 0;
    }
    private static void CheckForms()
    {
        using var settings=new MainForm();
        var shelf=(ReadingShelf)Get(settings,"_readingShelf")!;
        var list=(ListBox)Get(settings,"_historyList")!;
        var search=(TextBox)Get(settings,"_historySearch")!;
        var last=(TextBox)Get(settings,"_last")!;

        Check(list.AccessibleName?.Contains("最近読み取った") == true,
            "history list has a screen reader accessible name");
        Check(list.Font.Bold,"history list has bold Japanese font");
        Call(settings,"RecordRead","範囲OCR","一行目\n二行目");
        Check(shelf.Entries.Count==1 && list.Items.Count==1,
            "OCR result is visible in the WinForms history list");
        Check(last.Text=="","recording history does not overwrite OCR diagnostics");
        Call(settings,"RecordRead","範囲OCR","一行目\n二行目");
        Check(list.Items.Count==1,"duplicate input does not duplicate UI list");
        Call(settings,"RecordRead","漫画OCR","漫画の文字");
        Check(list.Items.Count==2 && list.SelectedIndex>=0,
            "two sources appear in history");
        search.Text="漫画";
        Check(list.Items.Count==1 && list.Items[0] is ReadingEntry e &&
              e.Text=="漫画の文字","search updates list and filters by Japanese text");
        search.Text="存在しないキーワード";
        Check(list.Items.Count==0,"no-match search shows empty list");
        search.Text="";
        Check(list.Items.Count==2,"clearing search restores entries");

        // Explicitly select an old entry and show in the large-text window.
        list.SelectedIndex=1;
        Call(settings,"OpenReadingWindow",true);
        var reader=(Form)Get(settings,"_readerWindow")!;
        var content=(RichTextBox)Get(settings,"_readerContent")!;
        Check(reader.Visible && content.ReadOnly && content.WordWrap,
            "large reader opens with read-only wrapped text");
        Check(content.Text=="一行目\n二行目","history selection sends correct text to reader");
        Check(content.Font.Bold && content.Font.Size>=24,
            "large reader uses bold Japanese font size");
        Check(content.BackColor==Color.Black && content.ForeColor==Color.White,
            "large reader starts in white-on-black mode");
        Set(settings,"_readerReverse",false);
        Call(settings,"ApplyReaderStyle");
        Check(content.BackColor==Color.White && content.ForeColor==Color.Black,
            "reader reverses contrast to black-on-white");
        Set(settings,"_readerFontSize",60f);
        Call(settings,"ApplyReaderStyle");
        Check(content.Font.Size>=59.9f,"reader supports 60-point font");
        Set(settings,"_readerFontSize",16f);
        Call(settings,"ApplyReaderStyle");
        Check(content.Font.Size<=16.1f,"reader supports 16-point font");
        // Changing list selection while the window is open must update the text.
        list.SelectedIndex=0;
        Check(content.Text=="漫画の文字","open reader follows selected history item");
        Call(settings,"OpenReadingHistoryWindow");
        var history=(Form)Get(settings,"_historyWindow")!;
        Check(history.Visible && history.ClientSize.Width>=590,
            "history opens in large independent window");
        Check(list.FindForm()==history,
            "existing list is hosted in big window without duplicating state");
        history.Close();
        Check(list.FindForm()==settings,
            "history controls return to settings when large window closes");

        Set(settings,"_lastText","秘密の古い文章");
        last.Text="秘密の古い文章";
        Call(settings,"ClearReadingHistory");
        Check(shelf.Entries.Count==0 && list.Items.Count==0,
            "clear erases every history entry and list item");
        Check(((string)Get(settings,"_lastText")!)=="", "clear erases cached last text");
        Check(last.Text=="" && content.Text=="" ,
            "clear also wipes last-text box and open reader content");
        reader.Close();
        Check(GetNullable(settings,"_readerWindow")==null,
            "reader window is disposed and reference cleared on close");
    }
    private static object? GetNullable(object form,string field)
    {
        var info=form.GetType().GetField(field,Private)
            ?? throw new MissingFieldException(field);
        return info.GetValue(form);
    }
}
