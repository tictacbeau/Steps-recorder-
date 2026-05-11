using System.Text.Json;
using System.Text.RegularExpressions;
using StepsRecorder.Core.Exporters;
using StepsRecorder.Core.Session;
using StepsRecorder.Core.Steps.Models;
using Xunit;

namespace StepsRecorder.Tests;

public class StepFormatterTests
{
    [Fact]
    public void JsonExporter_WritesWellFormedJson_With20Events()
    {
        var data = BuildFakeSession(20);
        var dir = CreateTempDir();
        try
        {
            JsonExporter.Export(data, dir);

            var json = File.ReadAllText(Path.Combine(dir, "session.json"));
            Assert.False(string.IsNullOrWhiteSpace(json));

            var doc = JsonDocument.Parse(json); // throws if malformed
            var events = doc.RootElement.GetProperty("events");
            Assert.Equal(20, events.GetArrayLength());

            // Each event should have required fields
            foreach (var evt in events.EnumerateArray())
            {
                Assert.True(evt.TryGetProperty("sequenceNumber", out _));
                Assert.True(evt.TryGetProperty("eventType", out _));
                Assert.True(evt.TryGetProperty("sessionMs", out _));
            }
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void HtmlExporter_WritesValidHtml_With20Steps()
    {
        var data = BuildFakeSession(20);
        var dir = CreateTempDir();
        try
        {
            HtmlExporter.Export(data, dir);

            var html = File.ReadAllText(Path.Combine(dir, "report.html"));
            Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);

            // 20 step divs
            int stepCount = Regex.Matches(html, @"class=""step""").Count;
            Assert.Equal(20, stepCount);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MarkdownExporter_WritesMarkdown_With20Steps()
    {
        var data = BuildFakeSession(20);
        var dir = CreateTempDir();
        try
        {
            MarkdownExporter.Export(data, dir);

            var md = File.ReadAllText(Path.Combine(dir, "report.md"));
            Assert.Contains("# Steps Recorder", md);

            int stepHeaders = Regex.Matches(md, @"^## Step \d{4}", RegexOptions.Multiline).Count;
            Assert.Equal(20, stepHeaders);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void JsonExporter_SessionIds_ArePreserved()
    {
        var data = BuildFakeSession(5);
        data.Events.Clear();
        data.Events.AddRange(BuildFakeEvents(5));

        var dir = CreateTempDir();
        try
        {
            JsonExporter.Export(data, dir);
            var json = File.ReadAllText(Path.Combine(dir, "session.json"));
            Assert.Contains(data.SessionId, json);
        }
        finally { Directory.Delete(dir, true); }
    }

    // -------------------------------------------------------

    private static SessionData BuildFakeSession(int count)
    {
        var data = new SessionData
        {
            SessionId = "2024-01-15_143022",
            StartTime = new DateTime(2024, 1, 15, 14, 30, 22, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 1, 15, 14, 31, 07, DateTimeKind.Utc),
            DurationMs = 45_000,
            EncoderUsed = "libx264",
            VideoFile = "recording.mp4"
        };
        data.Events.AddRange(BuildFakeEvents(count));
        return data;
    }

    private static IEnumerable<StepEvent> BuildFakeEvents(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            bool isKey = i % 5 == 0;
            yield return new StepEvent
            {
                SequenceNumber = i,
                EventType = isKey ? StepEventType.KeyPress : StepEventType.MouseClick,
                Button = isKey ? MouseButton.None : MouseButton.Left,
                IsDoubleClick = i % 7 == 0,
                ScreenX = 500 + i * 10,
                ScreenY = 300 + i * 5,
                WindowX = 100 + i * 5,
                WindowY = 50 + i * 3,
                Key = isKey ? "Ctrl+S" : "",
                WindowTitle = $"Test Window {i}",
                ProcessName = "notepad",
                SessionMs = i * 1200L,
                WallTime = DateTime.UtcNow.AddMilliseconds(i * 1200),
                ScreenshotFile = null // no actual screenshots in unit tests
            };
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"StepsRecorderTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
