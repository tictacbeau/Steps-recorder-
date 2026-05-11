namespace StepsRecorder.Core.Settings;

public sealed class AppSettings
{
    public string OutputPath { get; set; } = "sessions";
    public HotkeySettings Hotkeys { get; set; } = new();
    public VideoSettings Video { get; set; } = new();
    public ScreenshotSettings Screenshots { get; set; } = new();
    public EncoderSettings Encoder { get; set; } = new();
}

public sealed class HotkeySettings
{
    public string StartStop { get; set; } = "F9";
    public string PauseSteps { get; set; } = "F10";
}

public sealed class VideoSettings
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 30;
}

public sealed class ScreenshotSettings
{
    public bool HighlightClicks { get; set; } = true;
    public int HighlightRadius { get; set; } = 20;
}

public sealed class EncoderSettings
{
    public bool PreferHardware { get; set; } = true;
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
}
