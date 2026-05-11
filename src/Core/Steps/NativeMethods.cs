using System.Drawing;
using System.Runtime.InteropServices;

namespace StepsRecorder.Core.Steps;

internal static partial class NativeMethods
{
    internal const int WH_MOUSE_LL = 14;
    internal const int WH_KEYBOARD_LL = 13;

    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_QUIT = 0x0012;
    internal const int WM_HOTKEY = 0x0312;

    internal const int VK_RETURN = 0x0D;
    internal const int VK_S = 0x53;
    internal const int VK_Z = 0x5A;
    internal const int VK_Y = 0x59;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left, top, right, bottom;
    }

    internal delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll")]
    internal static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll")]
    internal static partial nint DispatchMessage(ref MSG lpMsg);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostThreadMessage(uint idThread, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetWindowText(nint hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ScreenToClient(nint hWnd, ref POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int nIndex);

    internal const int SM_CXDOUBLECLK   = 36;
    internal const int SM_CYDOUBLECLK   = 37;
    internal const int SM_XVIRTUALSCREEN  = 76;
    internal const int SM_YVIRTUALSCREEN  = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

    internal static System.Drawing.Rectangle GetVirtualScreenBounds()
    {
        return new System.Drawing.Rectangle(
            GetSystemMetrics(SM_XVIRTUALSCREEN),
            GetSystemMetrics(SM_YVIRTUALSCREEN),
            GetSystemMetrics(SM_CXVIRTUALSCREEN),
            GetSystemMetrics(SM_CYVIRTUALSCREEN));
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

    internal const int VK_CONTROL = 0x11;
    internal const int VK_SHIFT   = 0x10;
    internal const int VK_MENU    = 0x12; // Alt

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();
}
