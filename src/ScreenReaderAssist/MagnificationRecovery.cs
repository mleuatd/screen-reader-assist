using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScreenReaderAssist;

/// <summary>
/// Repairs a stranded full-screen Windows Magnification API transform.
/// Never enables magnification. Normal startup leaves Windows Magnifier intact.
/// </summary>
public static class MagnificationRecovery
{
    public sealed record Result(string Status,bool Changed,string Detail)
    {
        public bool Successful => Status is "初期状態" or "復旧完了";
    }

    public interface IDriver
    {
        bool WindowsMagnifierRunning();
        bool Initialize();
        bool Read(out float factor,out int x,out int y);
        bool Reset();
        void Finish();
    }

    public static Result OnStartup() => Recover(new WindowsDriver(),force:false);
    public static Result ExplicitReset() => Recover(new WindowsDriver(),force:true);

    public static Result Recover(IDriver driver,bool force)
    {
        ArgumentNullException.ThrowIfNull(driver);
        try
        {
            // Windows built-in Magnifier may be relied on by a visually impaired user.
            // Do not unexpectedly undo its display configuration on app startup.
            if(!force && driver.WindowsMagnifierRunning())
                return new Result("スキップ",false,"Windows標準の拡大鏡が動作しているため変更しません。");
            if(!driver.Initialize())
                return new Result("失敗",false,"Windows拡大機能を初期化できませんでした。");
            try
            {
                if(!driver.Read(out float scale,out int x,out int y))
                    return new Result("失敗",false,"表示倍率を読み取れませんでした。");
                if(Math.Abs(scale-1f)<0.001f && x==0 && y==0)
                    return new Result("初期状態",false,"表示倍率と画面位置は既に標準です。");
                if(!driver.Reset())
                    return new Result("失敗",false,"Windowsに表示倍率の初期化を要求できませんでした。");
                if(!driver.Read(out float after,out int afterX,out int afterY) ||
                   Math.Abs(after-1f)>0.001f || afterX!=0 || afterY!=0)
                    return new Result("失敗",true,"復旧を要求しましたが、標準表示になったことを確認できませんでした。");
                return new Result("復旧完了",true,"表示倍率100％・画面位置0,0へ復旧しました。");
            }
            finally {driver.Finish();}
        }
        catch(Exception ex)
        {
            return new Result("失敗",false,ex.GetType().Name+": "+ex.Message);
        }
    }

    private sealed class WindowsDriver : IDriver
    {
        [DllImport("Magnification.dll",SetLastError=true)] private static extern bool MagInitialize();
        [DllImport("Magnification.dll",SetLastError=true)] private static extern bool MagUninitialize();
        [DllImport("Magnification.dll",SetLastError=true)] private static extern bool MagGetFullscreenTransform(out float factor,out int x,out int y);
        [DllImport("Magnification.dll",SetLastError=true)] private static extern bool MagSetFullscreenTransform(float factor,int x,int y);

        public bool WindowsMagnifierRunning()
        {
            // Process.GetProcessesByName may fail in restricted environments:
            // fail safely (do not reset) in that case.
            try
            {
                using var processes=new ProcessCollection(Process.GetProcessesByName("Magnify"));
                return processes.Any;
            }
            catch {return true;}
        }
        public bool Initialize()=>MagInitialize();
        public bool Read(out float factor,out int x,out int y)=>MagGetFullscreenTransform(out factor,out x,out y);
        public bool Reset()=>MagSetFullscreenTransform(1.0f,0,0);
        public void Finish()=>MagUninitialize();

        private sealed class ProcessCollection : IDisposable
        {
            private readonly Process[] _items;
            public ProcessCollection(Process[] items) => _items=items;
            public bool Any=>_items.Length>0;
            public void Dispose(){foreach(var item in _items)item.Dispose();}
        }
    }
}
