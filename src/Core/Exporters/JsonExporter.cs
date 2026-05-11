using System.Text.Json;
using StepsRecorder.Core.Session;

namespace StepsRecorder.Core.Exporters;

public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static void Export(SessionData data, string sessionFolder)
    {
        var ordered = data.Events.OrderBy(e => e.SequenceNumber).ToList();

        var doc = new
        {
            sessionId = data.SessionId,
            startTime = data.StartTime,
            endTime = data.EndTime,
            durationMs = data.DurationMs,
            encoderUsed = data.EncoderUsed,
            videoFile = data.VideoFile,
            eventCount = ordered.Count,
            events = ordered
        };

        var json = JsonSerializer.Serialize(doc, Options);
        File.WriteAllText(Path.Combine(sessionFolder, "session.json"), json);
    }
}
