using System.Diagnostics;

namespace StepsRecorder.Core.Encoding;

public static class EncoderDetector
{
    private static readonly string[] HardwareEncoders = ["h264_nvenc", "h264_amf", "h264_qsv"];

    public static string Detect(string ffmpegPath, bool preferHardware)
    {
        if (!preferHardware) return "libx264";

        try
        {
            string resolved = ResolveFfmpegPath(ffmpegPath);
            if (!File.Exists(resolved)) return "libx264";

            var psi = new ProcessStartInfo
            {
                FileName = resolved,
                Arguments = "-encoders -v quiet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return "libx264";

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5_000);

            foreach (var enc in HardwareEncoders)
            {
                if (output.Contains(enc, StringComparison.OrdinalIgnoreCase))
                    return enc;
            }
        }
        catch { }

        return "libx264";
    }

    public static string ResolveFfmpegPath(string ffmpegPath)
    {
        if (Path.IsPathRooted(ffmpegPath)) return ffmpegPath;
        return Path.Combine(AppContext.BaseDirectory, ffmpegPath);
    }
}
