using System.Drawing;
using System.Runtime.InteropServices;
namespace ScreenReaderAssist;

public sealed class MagnificationService : IDisposable
{
    [DllImport("Magnification.dll", SetLastError=true)] private static extern bool MagInitialize();
    [DllImport("Magnification.dll", SetLastError=true)] private static extern bool MagUninitialize();
    [DllImport("Magnification.dll", SetLastError=true)] private static extern bool MagSetFullscreenTransform(float factor,int x,int y);
    [DllImport("Magnification.dll", SetLastError=true)] private static extern bool MagGetFullscreenTransform(out float factor,out int x,out int y);
    private readonly bool _ready;
    private float _oldLevel; private int _oldX,_oldY;
    private bool _active;
    private Point _origin;
    private int _baseX,_baseY;
    private float _zoom;
    public bool Active=>_active;
    public MagnificationService() { try { _ready=MagInitialize(); } catch(DllNotFoundException) { _ready=false; } }
    public bool Start(Point point,float zoom)
    {
        if(!_ready || _active || !MagGetFullscreenTransform(out _oldLevel,out _oldX,out _oldY)) return false;
        // Avoid overriding a zoom already controlled by Windows Magnifier.
        if(_oldLevel>1.001f) return false;
        var b=Screen.PrimaryScreen?.Bounds ?? ScreenCapture.VirtualBounds;
        _zoom=Math.Clamp(zoom,1.5f,6);
        _origin=point;
        _baseX=(int)Math.Round(point.X-(b.Width/2f)/_zoom);
        _baseY=(int)Math.Round(point.Y-(b.Height/2f)/_zoom);
        if(!MagSetFullscreenTransform(_zoom,_baseX,_baseY)) return false;
        _active=true; return true;
    }
    public void Drag(Point point)
    {
        if(!_active) return;
        // Screen-space mouse delta converted to unmagnified offset.
        var dx=(int)((point.X-_origin.X)/_zoom);
        var dy=(int)((point.Y-_origin.Y)/_zoom);
        MagSetFullscreenTransform(_zoom,_baseX-dx,_baseY-dy);
    }
    public void Restore()
    {
        if(!_active) return;
        MagSetFullscreenTransform(_oldLevel,_oldX,_oldY);
        _active=false;
    }
    public void Dispose() { Restore(); if(_ready) MagUninitialize(); }
}
