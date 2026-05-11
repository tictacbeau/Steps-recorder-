using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using StepsRecorder.Core.Settings;
using StepsRecorder.Core.Steps.Models;

namespace StepsRecorder.Core.Steps;

public sealed class StepsService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly InputHook _hook;
    private readonly ScreenshotService _screenshots;

    private readonly ConcurrentBag<StepEvent> _events = [];
    private readonly Channel<WorkItem> _workChannel;

    private string _sessionFolder = "";
    private Stopwatch _stopwatch = new();
    private int _seq;
    private Task? _workerTask;
    private CancellationTokenSource _cts = new();

    public IReadOnlyCollection<StepEvent> Events => _events;

    public StepsService(AppSettings settings)
    {
        _settings = settings;
        _hook = new InputHook();
        _screenshots = new ScreenshotService(settings);
        _workChannel = Channel.CreateBounded<WorkItem>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });

        _hook.MouseEvent += OnMouseEvent;
        _hook.KeyboardEvent += OnKeyboardEvent;
    }

    public void StartSession(string sessionFolder, Stopwatch sharedStopwatch)
    {
        _events.Clear();
        _sessionFolder = sessionFolder;
        _stopwatch = sharedStopwatch;
        _seq = 0;
        _cts = new CancellationTokenSource();

        _workerTask = Task.Run(() => ProcessWorkItemsAsync(_cts.Token));
        _hook.Start();
    }

    public void StopSession()
    {
        _hook.Stop();
        _workChannel.Writer.TryComplete();
        _workerTask?.Wait(5_000);
        _cts.Cancel();
    }

    public void SetPaused(bool paused) => _hook.SetPaused(paused);

    private void OnMouseEvent(MouseHookArgs args)
    {
        long ms = _stopwatch.ElapsedMilliseconds;
        var item = new WorkItem
        {
            Type = WorkItemType.Mouse,
            MouseArgs = args,
            SessionMs = ms,
            WallTime = DateTime.UtcNow
        };
        _workChannel.Writer.TryWrite(item);
    }

    private void OnKeyboardEvent(KeyboardHookArgs args)
    {
        if (!ShouldCaptureKey(args)) return;

        long ms = _stopwatch.ElapsedMilliseconds;
        var item = new WorkItem
        {
            Type = WorkItemType.Keyboard,
            KeyArgs = args,
            SessionMs = ms,
            WallTime = DateTime.UtcNow
        };
        _workChannel.Writer.TryWrite(item);
    }

    private static bool ShouldCaptureKey(KeyboardHookArgs args)
    {
        uint vk = args.VkCode;

        // Enter
        if (vk == NativeMethods.VK_RETURN) return true;

        // Ctrl+S, Ctrl+Z, Ctrl+Y
        if (args.IsCtrl && (vk == NativeMethods.VK_S ||
                             vk == NativeMethods.VK_Z ||
                             vk == NativeMethods.VK_Y))
            return true;

        return false;
    }

    private async Task ProcessWorkItemsAsync(CancellationToken ct)
    {
        await foreach (var item in _workChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                int seq = Interlocked.Increment(ref _seq);
                StepEvent evt;

                if (item.Type == WorkItemType.Mouse)
                {
                    var (title, procName, winX, winY) =
                        WindowContext.GetActiveWindowInfo(item.MouseArgs.ScreenX, item.MouseArgs.ScreenY);

                    string? screenshotRel = null;
                    try
                    {
                        screenshotRel = _screenshots.CaptureAndAnnotate(
                            _sessionFolder, seq, item.SessionMs,
                            item.MouseArgs.ScreenX, item.MouseArgs.ScreenY);
                    }
                    catch { /* screenshot failure is non-fatal */ }

                    evt = new StepEvent
                    {
                        SequenceNumber = seq,
                        EventType = StepEventType.MouseClick,
                        Button = item.MouseArgs.Msg == NativeMethods.WM_LBUTTONDOWN ? MouseButton.Left : MouseButton.Right,
                        IsDoubleClick = item.MouseArgs.IsDoubleClick,
                        ScreenX = item.MouseArgs.ScreenX,
                        ScreenY = item.MouseArgs.ScreenY,
                        WindowX = winX,
                        WindowY = winY,
                        WindowTitle = title,
                        ProcessName = procName,
                        SessionMs = item.SessionMs,
                        WallTime = item.WallTime,
                        ScreenshotFile = screenshotRel
                    };
                }
                else
                {
                    var hwnd = NativeMethods.GetForegroundWindow();
                    var (title, procName, _, _) = WindowContext.GetActiveWindowInfo(0, 0);
                    string keyName = FormatKey(item.KeyArgs);

                    string? screenshotRel = null;
                    try
                    {
                        // For key events, annotate center of virtual screen
                        var screen = NativeMethods.GetVirtualScreenBounds();
                        screenshotRel = _screenshots.CaptureAndAnnotate(
                            _sessionFolder, seq, item.SessionMs,
                            screen.Left + screen.Width / 2,
                            screen.Top + screen.Height / 2);
                    }
                    catch { }

                    evt = new StepEvent
                    {
                        SequenceNumber = seq,
                        EventType = StepEventType.KeyPress,
                        Key = keyName,
                        WindowTitle = title,
                        ProcessName = procName,
                        SessionMs = item.SessionMs,
                        WallTime = item.WallTime,
                        ScreenshotFile = screenshotRel
                    };
                }

                _events.Add(evt);
            }
            catch { /* protect the worker loop */ }
        }
    }

    private static string FormatKey(KeyboardHookArgs args)
    {
        string key = args.VkCode switch
        {
            NativeMethods.VK_RETURN => "Enter",
            NativeMethods.VK_S => "S",
            NativeMethods.VK_Z => "Z",
            NativeMethods.VK_Y => "Y",
            _ => $"VK_{args.VkCode:X2}"
        };

        if (args.IsCtrl) key = "Ctrl+" + key;
        if (args.IsShift) key = "Shift+" + key;
        if (args.IsAlt) key = "Alt+" + key;
        return key;
    }

    public void Dispose()
    {
        _hook.MouseEvent -= OnMouseEvent;
        _hook.KeyboardEvent -= OnKeyboardEvent;
        _hook.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private enum WorkItemType { Mouse, Keyboard }

    private struct WorkItem
    {
        public WorkItemType Type;
        public MouseHookArgs MouseArgs;
        public KeyboardHookArgs KeyArgs;
        public long SessionMs;
        public DateTime WallTime;
    }
}
