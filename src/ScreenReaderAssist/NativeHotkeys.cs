using System.Runtime.InteropServices;
namespace ScreenReaderAssist;
internal static class NativeHotkeys
{
    public const int WmHotkey=0x0312, Alt=0x0001, Control=0x0002, Shift=0x0004, NoRepeat=0x4000;
    [DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(nint hwnd,int id,int modifiers,int key);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool UnregisterHotKey(nint hwnd,int id);
}
