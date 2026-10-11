using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ScreenReaderAssist;

/// <summary>Observes clicks; deliberately never consumes them. First two clicks reach the underlying app.</summary>
public sealed class MouseTripleClickHook : IDisposable
{
    private const int WhMouseLl=14, WmLeftDown=0x0201,WmLeftUp=0x0202,WmMouseMove=0x0200;
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public Point pt; public uint mouseData,flags,time; public nint extraInfo; }
    private delegate nint HookProc(int code,nint wParam,nint lParam);
    [DllImport("user32.dll",SetLastError=true)] private static extern nint SetWindowsHookEx(int id,HookProc cb,nint module,uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook,int code,nint wParam,nint lParam);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    private readonly HookProc _proc;
    private nint _handle;
    private int _clicks;
    private long _lastClick;
    private Point _lastPoint;
    private bool _dragZoom;
    public Func<Point,bool>? OnTripleDown {get;set;}
    public Action<Point>? OnDrag {get;set;}
    public Action? OnRelease {get;set;}
    public MouseTripleClickHook()
    {
        _proc=Callback;
        var mod=Process.GetCurrentProcess().MainModule?.ModuleName;
        _handle=SetWindowsHookEx(WhMouseLl,_proc,GetModuleHandle(mod),0);
        if(_handle==0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    private nint Callback(int code,nint type,nint info)
    {
        if(code>=0) {
            var msg=(int)type;
            var data=Marshal.PtrToStructure<MouseData>(info);
            try {
                if(msg==WmLeftDown) {
                    long now=Environment.TickCount64;
                    if(now-_lastClick>500 || Math.Abs(data.pt.X-_lastPoint.X)>24 || Math.Abs(data.pt.Y-_lastPoint.Y)>24) _clicks=0;
                    _lastClick=now; _lastPoint=data.pt; _clicks++;
                    if(_clicks==3) { _dragZoom=OnTripleDown?.Invoke(data.pt)==true; _clicks=0; }
                } else if(msg==WmMouseMove && _dragZoom) OnDrag?.Invoke(data.pt);
                else if(msg==WmLeftUp && _dragZoom) { _dragZoom=false; OnRelease?.Invoke(); }
            } catch { _dragZoom=false; OnRelease?.Invoke(); }
        }
        return CallNextHookEx(_handle,code,type,info);
    }
    public void Dispose() { if(_handle!=0){UnhookWindowsHookEx(_handle);_handle=0;} }
}
