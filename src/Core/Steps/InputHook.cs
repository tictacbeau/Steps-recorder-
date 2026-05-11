using System.Runtime.InteropServices;

namespace StepsRecorder.Core.Steps;

public readonly record struct MouseHookArgs(int Msg, int ScreenX, int ScreenY, bool IsDoubleClick);
public readonly record struct KeyboardHookArgs(uint VkCode, bool IsCtrl, bool IsShift, bool IsAlt);

public sealed class InputHook : IDisposable
{
    public event Action<MouseHookArgs>? MouseEvent;
    public event Action<KeyboardHookArgs>? KeyboardEvent;

    public bool IsRunning { get; private set; }

    private Thread? _hookThread;
    private uint _hookThreadId;
    private nint _mouseHook;
    private nint _keyboardHook;
    private volatile bool _paused;

    // GC-root the delegates — NEVER make these locals
    private NativeMethods.HookProc? _mouseProcDelegate;
    private NativeMethods.HookProc? _keyboardProcDelegate;

    // Double-click state
    private int _lastClickX;
    private int _lastClickY;
    private uint _lastClickTime;
    private bool _lastWasLeft;

    private readonly object _startLock = new();

    public void Start()
    {
        lock (_startLock)
        {
            if (IsRunning) return;

            var ready = new ManualResetEventSlim(false);
            _hookThread = new Thread(() => HookThreadProc(ready))
            {
                IsBackground = true,
                Name = "InputHookThread"
            };
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            ready.Wait(5_000);
            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_startLock)
        {
            if (!IsRunning) return;
            NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, 0, 0);
            _hookThread?.Join(3_000);
            IsRunning = false;
        }
    }

    public void SetPaused(bool paused) => _paused = paused;

    private void HookThreadProc(ManualResetEventSlim ready)
    {
        _hookThreadId = NativeMethods.GetCurrentThreadId();

        _mouseProcDelegate = MouseProc;
        _keyboardProcDelegate = KeyboardProc;

        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _mouseProcDelegate, IntPtr.Zero, 0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _keyboardProcDelegate, IntPtr.Zero, 0);

        ready.Set();

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_mouseHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
    }

    private nint MouseProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_paused)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;

            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN)
            {
                bool isDouble = msg == NativeMethods.WM_LBUTTONDOWN && DetectDoubleClick(info);
                MouseEvent?.Invoke(new MouseHookArgs(msg, info.pt.x, info.pt.y, isDouble));
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && !_paused && wParam == 0x0100 /* WM_KEYDOWN */)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // Use GetAsyncKeyState — safe to call from hook thread
            bool ctrl  = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
            bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT)   & 0x8000) != 0;
            bool alt   = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU)    & 0x8000) != 0;

            KeyboardEvent?.Invoke(new KeyboardHookArgs(info.vkCode, ctrl, shift, alt));
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private bool DetectDoubleClick(NativeMethods.MSLLHOOKSTRUCT info)
    {
        uint dblClickTime = NativeMethods.GetDoubleClickTime();
        int dblClickW = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXDOUBLECLK);
        int dblClickH = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYDOUBLECLK);

        bool timely = (info.time - _lastClickTime) <= dblClickTime;
        bool nearby = Math.Abs(info.pt.x - _lastClickX) <= dblClickW / 2 &&
                      Math.Abs(info.pt.y - _lastClickY) <= dblClickH / 2;
        bool wasLeft = _lastWasLeft;

        _lastClickX = info.pt.x;
        _lastClickY = info.pt.y;
        _lastClickTime = info.time;
        _lastWasLeft = true;

        return timely && nearby && wasLeft;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
