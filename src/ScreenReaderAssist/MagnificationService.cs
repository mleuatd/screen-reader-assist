using System.Drawing;

namespace ScreenReaderAssist;

/// <summary>
/// DISABLED. The former fullscreen-transform zoom can survive a forced process
/// termination and leave an inaccessible Windows desktop. Retained only as a
/// compatibility shim: it must never apply a system-wide display transform.
/// </summary>
[Obsolete("Unsafe fullscreen magnification is disabled. Use MagnificationRecovery for repair only.")]
public sealed class MagnificationService : IDisposable
{
    public bool Active=>false;
    public bool Start(Point point,float zoom)
    {
        DiagnosticLog.Warn("拡大機能","危険な全画面拡大は無効化されています。表示倍率は変更しません。");
        return false;
    }
    public void Drag(Point point) { }
    public void Restore() { }
    public void Dispose() { }
}
