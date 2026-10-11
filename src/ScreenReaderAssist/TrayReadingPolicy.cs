namespace ScreenReaderAssist;

/// <summary>Cursor reading is independent of the notification icon and active while UI is hidden.</summary>
public static class TrayReadingPolicy
{
    public static bool CanAutomaticallyRead(bool settingsWindowVisible,bool manualActionBusy,string? mode)
        => !settingsWindowVisible && !manualActionBusy && ReadingModes.IsAuto(mode);
}
