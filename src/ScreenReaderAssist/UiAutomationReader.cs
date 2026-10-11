namespace ScreenReaderAssist;
/// <summary>Read text exposed by the currently focused Windows UI Automation element.</summary>
public static class UiAutomationReader
{
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
