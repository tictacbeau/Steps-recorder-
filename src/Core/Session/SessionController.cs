using System.Diagnostics;
using StepsRecorder.Core.Encoding;
using StepsRecorder.Core.Exporters;
using StepsRecorder.Core.ScreenCapture;
using StepsRecorder.Core.Settings;
using StepsRecorder.Core.Steps;

namespace StepsRecorder.Core.Session;

public enum SessionState { Idle, Recording, PausedSteps }

public sealed class SessionController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly StepsService _steps;

    private IVideoEncoder? _encoder;
    private WgcScreenCapture? _capture;

    private readonly Stopwatch _stopwatch = new();
    private SessionData? _data;
    private Task? _encodeTask;
    private CancellationTokenSource _cts = new();

    public SessionState State { get; private set; } = SessionState.Idle;
    public event Action<SessionState>? StateChanged;

    public string? CurrentSessionFolder { get; private set; }

    public SessionController(AppSettings settings)
    {
        _settings = settings;
        _steps = new StepsService(settings);
    }

    public void StartSession()
    {
        if (State != SessionState.Idle) return;

        var startTime = DateTime.UtcNow;
        var sessionId = PathHelper.GetSessionId(startTime);
        var sessionFolder = PathHelper.EnsureSessionFolder(_settings.OutputPath, sessionId);
        CurrentSessionFolder = sessionFolder;

        _data = new SessionData
        {
            SessionId = sessionId,
            StartTime = startTime,
            VideoFile = "recording.mp4"
        };

        // Detect and create encoder
        string encoderName = EncoderDetector.Detect(
            _settings.Encoder.FfmpegPath, _settings.Encoder.PreferHardware);
        _data.EncoderUsed = encoderName;

        _encoder = new FfmpegVideoEncoder(_settings, encoderName);

        // Start shared stopwatch before everything else
        _stopwatch.Restart();

        // Start steps capture
        _steps.StartSession(sessionFolder, _stopwatch);

        // Start video encoder
        try
        {
            string mp4Path = Path.Combine(sessionFolder, "recording.mp4");
            _encoder.Start(mp4Path);

            // Start WGC capture
            _capture = new WgcScreenCapture(WgcScreenCapture.CreateForPrimaryMonitor());

            _cts = new CancellationTokenSource();
            _encodeTask = Task.Run(() => EncodeLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Video] Failed to start capture: {ex.Message}");
            // Video unavailable but steps still run
            _capture?.Dispose();
            _capture = null;
        }

        SetState(SessionState.Recording);
    }

    public void StopSession()
    {
        if (State == SessionState.Idle) return;

        // Stop steps first (flush remaining events)
        _steps.StopSession();

        // Stop encode loop
        _cts.Cancel();
        _capture?.StopCapture();

        try { _encodeTask?.Wait(10_000); }
        catch { }

        // Stop encoder (finalizes MP4)
        _encoder?.Stop();
        _encoder?.Dispose();
        _encoder = null;

        _capture?.Dispose();
        _capture = null;

        _stopwatch.Stop();

        // Populate final session data
        if (_data is not null)
        {
            _data.EndTime = DateTime.UtcNow;
            _data.DurationMs = _stopwatch.ElapsedMilliseconds;
            _data.Events.AddRange(_steps.Events.OrderBy(e => e.SequenceNumber));

            // Export
            var folder = CurrentSessionFolder!;
            try { JsonExporter.Export(_data, folder); } catch { }
            try { HtmlExporter.Export(_data, folder); } catch { }
            try { MarkdownExporter.Export(_data, folder); } catch { }
        }

        SetState(SessionState.Idle);
    }

    public void TogglePauseSteps()
    {
        if (State == SessionState.Idle) return;

        if (State == SessionState.Recording)
        {
            _steps.SetPaused(true);
            SetState(SessionState.PausedSteps);
        }
        else
        {
            _steps.SetPaused(false);
            SetState(SessionState.Recording);
        }
    }

    private async Task EncodeLoopAsync(CancellationToken ct)
    {
        if (_capture is null || _encoder is null) return;

        int frameBytes = _settings.Video.Width * _settings.Video.Height * 4;
        var buffer = new byte[frameBytes];
        int frameMs = 1_000 / _settings.Video.Fps;

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(frameMs));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(ct)) break;

                if (_capture.TryGetNextFrame(buffer, out long captureMs))
                {
                    _encoder.WriteFrame(buffer, _stopwatch.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* protect the loop */ }
        }
    }

    private void SetState(SessionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        if (State != SessionState.Idle)
            StopSession();

        _steps.Dispose();
        _capture?.Dispose();
        _encoder?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
