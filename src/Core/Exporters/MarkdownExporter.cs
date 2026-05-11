using System.Text;
using StepsRecorder.Core.Session;
using StepsRecorder.Core.Steps.Models;

namespace StepsRecorder.Core.Exporters;

public static class MarkdownExporter
{
    public static void Export(SessionData data, string sessionFolder)
    {
        var sb = new StringBuilder();
        var ordered = data.Events.OrderBy(e => e.SequenceNumber).ToList();

        sb.AppendLine($"# Steps Recorder — Session {data.SessionId}");
        sb.AppendLine();
        sb.AppendLine($"**Started:** {data.StartTime:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Duration:** {FormatDuration(data.DurationMs)}  ");
        sb.AppendLine($"**Steps:** {ordered.Count}  ");
        if (!string.IsNullOrEmpty(data.VideoFile))
            sb.AppendLine($"**Video:** [{data.VideoFile}]({data.VideoFile})  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var evt in ordered)
        {
            string timeLabel = FormatSessionTime(evt.SessionMs);
            string desc = DescribeEvent(evt);

            sb.AppendLine($"## Step {evt.SequenceNumber:D4} — {timeLabel}");
            sb.AppendLine();
            sb.AppendLine($"**Action:** {desc}  ");
            sb.AppendLine($"**Window:** {Escape(evt.WindowTitle)} ({Escape(evt.ProcessName)})  ");
            sb.AppendLine($"**Time:** {evt.WallTime:HH:mm:ss.fff} UTC  ");

            if (evt.EventType == StepEventType.MouseClick)
                sb.AppendLine($"**Position:** Screen ({evt.ScreenX}, {evt.ScreenY}) / Window ({evt.WindowX}, {evt.WindowY})  ");

            if (evt.ScreenshotFile != null)
            {
                sb.AppendLine();
                sb.AppendLine($"![Step {evt.SequenceNumber}]({evt.ScreenshotFile})");
            }

            sb.AppendLine();
        }

        File.WriteAllText(Path.Combine(sessionFolder, "report.md"), sb.ToString(), Encoding.UTF8);
    }

    private static string DescribeEvent(StepEvent evt) =>
        evt.EventType switch
        {
            StepEventType.MouseClick => evt.Button switch
            {
                Steps.Models.MouseButton.Left =>
                    evt.IsDoubleClick ? "Double-click (left)" : "Click (left)",
                Steps.Models.MouseButton.Right => "Click (right)",
                _ => "Mouse click"
            },
            StepEventType.KeyPress => $"Key press: {evt.Key}",
            _ => "Unknown"
        };

    private static string Escape(string s) => s.Replace("|", "\\|");

    private static string FormatSessionTime(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static string FormatDuration(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
    }
}
