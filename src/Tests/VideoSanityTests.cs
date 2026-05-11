using System.Runtime.InteropServices;
using StepsRecorder.Core.Encoding;
using StepsRecorder.Core.Settings;
using Xunit;

namespace StepsRecorder.Tests;

public class VideoSanityTests
{
    [Fact]
    public async Task Record300Frames_ProducesNonEmptyMp4()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Skip on non-Windows (CI Linux runners, etc.)
            return;
        }

        var settings = new AppSettings
        {
            Video = new VideoSettings { Width = 640, Height = 360, Fps = 30 },
            Encoder = new EncoderSettings { PreferHardware = false, FfmpegPath = "ffmpeg.exe" }
        };

        // Resolve ffmpeg from EXE directory or PATH
        string ffmpegPath = settings.Encoder.FfmpegPath;
        string resolved = EncoderDetector.ResolveFfmpegPath(ffmpegPath);
        if (!File.Exists(resolved))
        {
            // Try PATH
            bool onPath = Environment.GetEnvironmentVariable("PATH")
                ?.Split(Path.PathSeparator)
                .Any(p => File.Exists(Path.Combine(p, "ffmpeg.exe"))) ?? false;

            if (!onPath)
            {
                // ffmpeg not available — skip gracefully
                return;
            }
        }

        string encoder = EncoderDetector.Detect(ffmpegPath, preferHardware: false);
        var outputPath = Path.Combine(Path.GetTempPath(), $"sr_test_{Guid.NewGuid():N}.mp4");

        try
        {
            using var enc = new FfmpegVideoEncoder(settings, encoder);
            enc.Start(outputPath);

            int w = settings.Video.Width;
            int h = settings.Video.Height;
            int frameSize = w * h * 4;
            var frame = new byte[frameSize];

            // 300 frames = 10 seconds at 30fps
            for (int i = 0; i < 300; i++)
            {
                // Simple gradient pattern so each frame differs
                byte luma = (byte)(i % 256);
                Array.Fill(frame, luma);
                enc.WriteFrame(frame, i * 33L);
                await Task.Yield(); // keep async context alive
            }

            enc.Stop();

            Assert.True(File.Exists(outputPath), "MP4 file was not created");
            var info = new FileInfo(outputPath);
            Assert.True(info.Length > 1024, $"MP4 too small ({info.Length} bytes); encoding may have failed");
        }
        finally
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void EncoderDetector_ReturnsLibx264_WhenNoHardware()
    {
        // With preferHardware=false, must always return libx264
        string result = EncoderDetector.Detect("ffmpeg.exe", preferHardware: false);
        Assert.Equal("libx264", result);
    }

    [Fact]
    public void EncoderDetector_ReturnsFallback_WhenFfmpegMissing()
    {
        // Non-existent path should fall back to libx264 gracefully
        string result = EncoderDetector.Detect("/nonexistent/ffmpeg.exe", preferHardware: true);
        Assert.Equal("libx264", result);
    }
}
