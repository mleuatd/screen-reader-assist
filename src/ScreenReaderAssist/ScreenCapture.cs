using System.Drawing;
namespace ScreenReaderAssist;
public static class ScreenCapture
{
    public static Rectangle VirtualBounds => SystemInformation.VirtualScreen;
    public static Bitmap Capture(Rectangle bounds)
    {
        if (bounds.Width < 1 || bounds.Height < 1) throw new ArgumentOutOfRangeException(nameof(bounds));
        var bmp = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try {
            using var graphics = Graphics.FromImage(bmp);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        } catch { bmp.Dispose(); throw; }
    }
}
