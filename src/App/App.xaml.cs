using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using H.NotifyIcon;
using StepsRecorder.App.Views;
using StepsRecorder.Core.Session;
using StepsRecorder.Core.Settings;

namespace StepsRecorder.App;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private StatusBarWindow? _statusBar;
    private SessionController? _controller;
    private AppSettings _settings = new();

    private HwndSource? _hwndSource;
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyIdStartStop = 1;
    private const int HotkeyIdPause = 2;

    // VK codes
    private const uint VK_F9 = 0x78;
    private const uint VK_F10 = 0x79;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = SettingsManager.Load();
        _controller = new SessionController(_settings);
        _controller.StateChanged += OnStateChanged;

        // Tray icon
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

        // Status bar (hidden until recording starts)
        _statusBar = new StatusBarWindow();

        RegisterGlobalHotkeys();
    }

    private void RegisterGlobalHotkeys()
    {
        var hwndParams = new HwndSourceParameters("HotkeyHelper")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0x00000080 // WS_EX_TOOLWINDOW
        };
        _hwndSource = new HwndSource(hwndParams);
        _hwndSource.AddHook(WndProc);

        bool f9ok = NativeMethods.RegisterHotKey(_hwndSource.Handle, HotkeyIdStartStop, 0, VK_F9);
        bool f10ok = NativeMethods.RegisterHotKey(_hwndSource.Handle, HotkeyIdPause, 0, VK_F10);

        if (!f9ok || !f10ok)
        {
            // Hotkey conflict — user can still use the tray context menu
            System.Diagnostics.Debug.WriteLine(
                "[StepsRecorder] Warning: could not register F9/F10 hotkeys (in use by another app)");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HotkeyIdStartStop)
            {
                ToggleRecording();
                handled = true;
            }
            else if (id == HotkeyIdPause)
            {
                _controller?.TogglePauseSteps();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ToggleRecording()
    {
        if (_controller is null) return;
        if (_controller.State == SessionState.Idle)
            StartRecording();
        else
            StopRecording();
    }

    private void StartRecording()
    {
        _controller!.StartSession();
        _statusBar?.Show();
    }

    private void StopRecording()
    {
        _controller!.StopSession();
        _statusBar?.Hide();

        // Open output folder after stop
        var folder = _controller.CurrentSessionFolder;
        if (folder is not null && System.IO.Directory.Exists(folder))
        {
            try { Process.Start("explorer.exe", folder); }
            catch { }
        }
    }

    private void OnStateChanged(SessionState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            // Update status bar
            _statusBar?.ViewModel.OnStateChanged(state);

            // Update tray tooltip and menu
            UpdateTrayMenu(state);
        });
    }

    private void UpdateTrayMenu(SessionState state)
    {
        bool recording = state != SessionState.Idle;
        if (_trayIcon?.ContextMenu is { } menu)
        {
            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                switch (item.Name)
                {
                    case "MenuStart":  item.IsEnabled = !recording; break;
                    case "MenuStop":   item.IsEnabled = recording;  break;
                    case "MenuPause":  item.IsEnabled = recording;  break;
                }
            }
        }

        if (_trayIcon is not null)
        {
            _trayIcon.ToolTipText = state switch
            {
                SessionState.Recording   => "Steps Recorder — Recording",
                SessionState.PausedSteps => "Steps Recorder — Steps Paused",
                _                        => "Steps Recorder — Idle"
            };
        }
    }

    // ----- Menu handlers -----

    private void MenuStart_Click(object sender, RoutedEventArgs e) => StartRecording();

    private void MenuStop_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void MenuPause_Click(object sender, RoutedEventArgs e) =>
        _controller?.TogglePauseSteps();

    private void MenuFolder_Click(object sender, RoutedEventArgs e)
    {
        string folder = _controller?.CurrentSessionFolder
            ?? _settings.OutputPath;

        if (!System.IO.Path.IsPathRooted(folder))
            folder = System.IO.Path.Combine(AppContext.BaseDirectory, folder);

        System.IO.Directory.CreateDirectory(folder);
        try { Process.Start("explorer.exe", folder); }
        catch { }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (_controller?.State != SessionState.Idle)
            StopRecording();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_hwndSource is not null)
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyIdStartStop);
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyIdPause);
            _hwndSource.Dispose();
        }

        _controller?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
