using System.Net;
using System.Text;
using StepsRecorder.Core.Session;
using StepsRecorder.Core.Steps.Models;

namespace StepsRecorder.Core.Exporters;

public static class HtmlExporter
{
    public static void Export(SessionData data, string sessionFolder)
    {
        var sb = new StringBuilder();
        var ordered = data.Events.OrderBy(e => e.SequenceNumber).ToList();

        sb.AppendLine("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Steps Recorder Report</title>
              <style>
                *{box-sizing:border-box;margin:0;padding:0}
                body{font-family:'Segoe UI',system-ui,sans-serif;background:#f0f2f5;color:#222;padding:2rem}
                h1{font-size:1.6rem;margin-bottom:.25rem}
                .meta{font-size:.85rem;color:#666;margin-bottom:1.5rem}
                .meta a{color:#0078d4;text-decoration:none}
                video{width:100%;max-height:540px;border-radius:8px;background:#000;margin-bottom:1.5rem;display:block}
                .step{background:#fff;border-radius:10px;padding:1rem 1.25rem;margin:.75rem 0;
                      box-shadow:0 1px 4px rgba(0,0,0,.1);transition:box-shadow .15s}
                .step:hover{box-shadow:0 3px 10px rgba(0,0,0,.15)}
                .step-hdr{display:flex;flex-wrap:wrap;gap:.5rem;align-items:baseline;margin-bottom:.4rem}
                .seq{font-weight:700;color:#555;font-size:.9rem;min-width:3.5rem}
                .timelink{color:#0078d4;font-size:.85rem;cursor:pointer;text-decoration:underline;white-space:nowrap}
                .action{font-weight:600;font-size:.95rem;flex:1}
                .wnd{font-size:.8rem;color:#888;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:40ch}
                .coords{font-size:.75rem;color:#aaa;font-family:monospace}
                .step img{max-width:100%;border:1px solid #ddd;border-radius:6px;margin-top:.6rem;display:block;
                           cursor:zoom-in}
                .step img:hover{border-color:#0078d4}
                #lightbox{display:none;position:fixed;inset:0;background:rgba(0,0,0,.85);z-index:9999;
                           align-items:center;justify-content:center;cursor:zoom-out}
                #lightbox img{max-width:95vw;max-height:95vh;border-radius:6px}
                @media(max-width:600px){body{padding:1rem}.step{padding:.75rem}}
              </style>
            </head>
            <body>
            """);

        // Header
        string duration = FormatDuration(data.DurationMs);
        sb.AppendLine($"<h1>Steps Recorder — {WebUtility.HtmlEncode(data.SessionId)}</h1>");
        sb.AppendLine($"""<p class="meta">Started {data.StartTime:yyyy-MM-dd HH:mm:ss} UTC &nbsp;·&nbsp; Duration {duration} &nbsp;·&nbsp; {ordered.Count} step{(ordered.Count == 1 ? "" : "s")}</p>""");

        // Video (relative link, NOT base64 — MP4 can be huge)
        string videoPath = Path.Combine(sessionFolder, data.VideoFile);
        if (!string.IsNullOrEmpty(data.VideoFile) && File.Exists(videoPath))
        {
            sb.AppendLine($"""<video controls preload="metadata" id="mainvideo"><source src="{WebUtility.HtmlEncode(data.VideoFile)}" type="video/mp4">Your browser does not support video.</video>""");
        }

        // Steps
        foreach (var evt in ordered)
        {
            string timeLabel = FormatSessionTime(evt.SessionMs);
            double videoSec = evt.SessionMs / 1000.0;
            string action = DescribeEvent(evt);

            string imgTag = "";
            if (evt.ScreenshotFile is not null)
            {
                string imgAbsPath = Path.Combine(sessionFolder, evt.ScreenshotFile);
                if (File.Exists(imgAbsPath))
                {
                    string b64 = Convert.ToBase64String(File.ReadAllBytes(imgAbsPath));
                    imgTag = $"""<img src="data:image/png;base64,{b64}" alt="Step {evt.SequenceNumber}" loading="lazy" onclick="openLightbox(this)">""";
                }
            }

            string coordsHtml = evt.EventType == StepEventType.MouseClick
                ? $"""<span class="coords">({evt.ScreenX},{evt.ScreenY})</span>"""
                : "";

            sb.AppendLine($"""
                <div class="step" id="step-{evt.SequenceNumber}">
                  <div class="step-hdr">
                    <span class="seq">#{evt.SequenceNumber:D4}</span>
                    <span class="timelink" onclick="seekTo({videoSec:F3})" title="Jump to this moment in video">at {WebUtility.HtmlEncode(timeLabel)}</span>
                    <span class="action">{WebUtility.HtmlEncode(action)}</span>
                    {coordsHtml}
                  </div>
                  <div class="wnd" title="{WebUtility.HtmlEncode(evt.WindowTitle)}">{WebUtility.HtmlEncode(evt.WindowTitle)} — {WebUtility.HtmlEncode(evt.ProcessName)}</div>
                  {imgTag}
                </div>
                """);
        }

        // Lightbox + seek script
        sb.AppendLine("""
            <div id="lightbox" onclick="this.style.display='none'">
              <img id="lbimg" src="" alt="screenshot">
            </div>
            <script>
              const video = document.getElementById('mainvideo');
              function seekTo(t){if(video){video.currentTime=t;video.play();}}
              function openLightbox(img){
                document.getElementById('lbimg').src=img.src;
                document.getElementById('lightbox').style.display='flex';
              }
              document.addEventListener('keydown',e=>{if(e.key==='Escape')document.getElementById('lightbox').style.display='none';});
            </script>
            </body></html>
            """);

        File.WriteAllText(
            Path.Combine(sessionFolder, "report.html"),
            sb.ToString(),
            Encoding.UTF8);
    }

    private static string DescribeEvent(StepEvent evt) =>
        evt.EventType switch
        {
            StepEventType.MouseClick => evt.Button switch
            {
                MouseButton.Left => evt.IsDoubleClick ? "Double-click (left button)" : "Left click",
                MouseButton.Right => "Right click",
                _ => "Mouse click"
            },
            StepEventType.KeyPress => $"Key: {evt.Key}",
            _ => "Action"
        };

    private static string FormatSessionTime(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static string FormatDuration(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
            : $"{ts.Seconds}.{ts.Milliseconds / 100}s";
    }
}
