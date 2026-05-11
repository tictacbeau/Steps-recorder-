using StepsRecorder.Core.Steps.Models;

namespace StepsRecorder.Core.Session;

public sealed class SessionData
{
    public string SessionId { get; init; } = "";
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; set; }
    public long DurationMs { get; set; }
    public string EncoderUsed { get; set; } = "";
    public string VideoFile { get; set; } = "";
    public List<StepEvent> Events { get; init; } = [];
}
