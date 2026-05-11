using System.Drawing;
using System.Drawing.Imaging;
using StepsRecorder.Core.Settings;
using StepsRecorder.Core.Session;

namespace StepsRecorder.Core.Steps;

public sealed class ScreenshotService
{
    private readonly AppSettings _settings;

    public ScreenshotService(AppSettings settings)
    {
        _settings = settings;
    }

    public string CaptureAndAnnotate(
        string sessionFolder,
        int sequenceNumber,
        long sessionMs,
        int cursorX,
        int cursorY)
    {
        var screenshotsDir = PathHelper.ScreenshotsFolder(sessionFolder);
        Directory.CreateDirectory(screenshotsDir);

        var fileName = PathHelper.ScreenshotFileName(sequenceNumber, sessionMs);
        var filePath = Path.Combine(screenshotsDir, fileName);

        using var bmp = CaptureScreen();

        if (_settings.Screenshots.HighlightClicks)
        {
            var bounds = NativeMethods.GetVirtualScreenBounds();
            using var g = Graphics.FromImage(bmp);
            DrawHighlight(g,
                cursorX - bounds.X,
                cursorY - bounds.Y,
                _settings.Screenshots.HighlightRadius);
        }

        bmp.Save(filePath, ImageFormat.Png);
        return Path.Combine("screenshots", fileName);
    }

    private static Bitmap CaptureScreen()
    {
        var bounds = NativeMethods.GetVirtualScreenBounds();
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    private static void DrawHighlight(Graphics g, int x, int y, int radius)
    {
        using var pen = new Pen(Color.Red, 3f);
        g.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);

        using var fill = new SolidBrush(Color.FromArgb(128, Color.Red));
        g.FillEllipse(fill, x - 5, y - 5, 10, 10);
    }
}
