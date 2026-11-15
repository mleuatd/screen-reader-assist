using System.Drawing;
namespace ScreenReaderAssist;

/// <summary>
/// Final safety decision after asynchronous hover OCR returns.
/// A user action during OCR must not result in speech over a drag or click.
/// </summary>
public static class HoverSpeechGuard
{
    public static bool CanDeliver(
        Point sampledPosition, Point currentPosition,
        int sampledGeneration, int currentGeneration,
        bool anyMouseButtonHeld, bool manuallyReading, bool settingsVisible,
        bool automaticReadingEnabled, bool requestedModeStillActive)
    {
        return !anyMouseButtonHeld && !manuallyReading && !settingsVisible &&
               automaticReadingEnabled && requestedModeStillActive &&
               sampledGeneration==currentGeneration &&
               Math.Abs((long)sampledPosition.X-currentPosition.X)<=10 &&
               Math.Abs((long)sampledPosition.Y-currentPosition.Y)<=10;
    }
}
