namespace StepsRecorder.Core.Steps.Models;

public enum StepEventType { MouseClick, KeyPress }

public enum MouseButton { None, Left, Right, Middle }

public sealed record StepEvent
{
    public int SequenceNumber { get; init; }
    public StepEventType EventType { get; init; }

    // Mouse fields
    public MouseButton Button { get; init; }
    public bool IsDoubleClick { get; init; }
    public int ScreenX { get; init; }
    public int ScreenY { get; init; }
    public int WindowX { get; init; }
    public int WindowY { get; init; }

    // Keyboard fields
    public string Key { get; init; } = "";

    // Window context
    public string WindowTitle { get; init; } = "";
    public string ProcessName { get; init; } = "";

    // Timing
    public long SessionMs { get; init; }
    public DateTime WallTime { get; init; }

    // Output
    public string? ScreenshotFile { get; init; }
}
