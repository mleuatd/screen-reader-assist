using System.Drawing;
using System.Runtime.InteropServices;
namespace ScreenReaderAssist;
/// <summary>Read text exposed by the currently focused Windows UI Automation element.</summary>
public static class UiAutomationReader
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ScreenPoint { public int x, y; public ScreenPoint(Point p) {x=p.X;y=p.Y;} }

    [DllImport("oleacc.dll", PreserveSig=true)]
    private static extern int AccessibleObjectFromPoint(ScreenPoint point,
        [MarshalAs(UnmanagedType.Interface)] out object accessible,
        [MarshalAs(UnmanagedType.Struct)] out object childId);

    /// <summary>Fallback for classic controls and browser text exposed through MSAA.</summary>
    public static string ReadMsaaAtPoint(Point point)
    {
        object? accessible=null;
        try {
            if(AccessibleObjectFromPoint(new ScreenPoint(point),out var obj,out var child)!=0) return "";
            accessible=obj;
            dynamic item=obj;
            string name="";
            try { name=Convert.ToString(item.accName(child))?.Trim() ?? ""; }
            catch { try { name=Convert.ToString(item.accName[child])?.Trim() ?? ""; } catch { } }
            if(name.Length>500) name=name[..500];
            return name;
        } catch { return ""; }
        finally {
            if(accessible!=null && Marshal.IsComObject(accessible))
                try { Marshal.ReleaseComObject(accessible); } catch { }
        }
    }

    /// <summary>Read accessible digital text under pointer, without clicking.</summary>
    public static string ReadAtPoint(Point point)
    {
        try {
            var type=Type.GetTypeFromCLSID(new Guid("FF48DBA4-60EF-4201-AA87-54103EEF594E"));
            if(type is null) return "";
            dynamic automation=Activator.CreateInstance(type)!;
            dynamic element=automation.ElementFromPoint(new ScreenPoint(point));
            if(element is null) return "";
            // Only actual text-bearing controls; skip panes and window titles.
            int kind=(int)element.CurrentControlType;
            if(kind is not (50000 or 50002 or 50003 or 50004 or 50005 or 50007 or 50011 or 50013 or 50019 or 50020 or 50029 or 50034))
                return "";
            string name=((string?)element.CurrentName ?? "").Trim();
            if(name.Length>0) return name.Length>500 ? name[..500] : name;
            if(kind==50004) {
                try {
                    dynamic pattern=element.GetCurrentPattern(10014);
                    string text=(string)pattern.DocumentRange.GetText(500);
                    return text.Trim();
                } catch { }
            }
        } catch { /* UIA may be unavailable or blocked on protected surfaces. */ }
        return "";
    }

    public static string ReadFocused()
    {
        // Native UI Automation COM, no external dependency or cloud account.
        try {
            var t=Type.GetTypeFromCLSID(new Guid("FF48DBA4-60EF-4201-AA87-54103EEF594E"));
            if(t is null) return "";
            dynamic automation=Activator.CreateInstance(t)!;
            dynamic element=automation.GetFocusedElement();
            if(element is null) return "";
            try {
                // UIA_TextPatternId = 10014. When present, prefer the entire focused text range.
                dynamic textPattern=element.GetCurrentPattern(10014);
                string text=(string)textPattern.DocumentRange.GetText(-1);
                if(!string.IsNullOrWhiteSpace(text)) return text.Trim();
            } catch { /* element does not expose a text range */ }
            try { return ((string)element.CurrentName ?? "").Trim(); } catch { return ""; }
        } catch { return ""; }
    }
}
