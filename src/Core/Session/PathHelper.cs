namespace StepsRecorder.Core.Session;

public static class PathHelper
{
    public static string GetSessionId(DateTime dt) =>
        dt.ToString("yyyy-MM-dd_HHmmss");

    public static string EnsureSessionFolder(string basePath, string sessionId)
    {
        // basePath is relative to EXE dir if not rooted
        if (!Path.IsPathRooted(basePath))
            basePath = Path.Combine(AppContext.BaseDirectory, basePath);

        var folder = Path.Combine(basePath, sessionId);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string ScreenshotsFolder(string sessionFolder) =>
        Path.Combine(sessionFolder, "screenshots");

    public static string ScreenshotFileName(int seq, long sessionMs) =>
        $"step_{seq:D4}_{sessionMs:D7}ms.png";
}
