using System.Diagnostics;
using System.Text;

namespace StepsRecorder.Core.Steps;

public static class WindowContext
{
    public static (string Title, string ProcessName, int WindowX, int WindowY)
        GetActiveWindowInfo(int screenX, int screenY)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return ("", "", screenX, screenY);

        var titleBuf = new StringBuilder(512);
        NativeMethods.GetWindowText(hwnd, titleBuf, titleBuf.Capacity);
        string title = titleBuf.ToString();

        string processName = "";
        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            using var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;
        }
        catch { }

        // Convert screen coords to window-client coords
        var pt = new NativeMethods.POINT { x = screenX, y = screenY };
        NativeMethods.ScreenToClient(hwnd, ref pt);

        return (title, processName, pt.x, pt.y);
    }
}
