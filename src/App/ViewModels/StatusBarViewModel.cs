using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using StepsRecorder.Core.Session;

namespace StepsRecorder.App.ViewModels;

public sealed class StatusBarViewModel : INotifyPropertyChanged
{
    private string _stateLabel = "Idle";
    private string _elapsedLabel = "00:00";
    private readonly Stopwatch _stopwatch = new();

    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    public string StateLabel
    {
        get => _stateLabel;
        private set { _stateLabel = value; OnPropertyChanged(); }
    }

    public string ElapsedLabel
    {
        get => _elapsedLabel;
        private set { _elapsedLabel = value; OnPropertyChanged(); }
    }

    public StatusBarViewModel()
    {
        _timer.Tick += (_, _) => UpdateElapsed();
    }

    public void OnStateChanged(SessionState state)
    {
        switch (state)
        {
            case SessionState.Recording:
                _stopwatch.Restart();
                _timer.Start();
                StateLabel = "● Recording";
                break;
            case SessionState.PausedSteps:
                StateLabel = "⏸ Steps Paused";
                // Timer keeps running; video still records
                break;
            case SessionState.Idle:
                _timer.Stop();
                _stopwatch.Reset();
                StateLabel = "Idle";
                ElapsedLabel = "00:00";
                break;
        }
    }

    private void UpdateElapsed()
    {
        var ts = _stopwatch.Elapsed;
        ElapsedLabel = $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
