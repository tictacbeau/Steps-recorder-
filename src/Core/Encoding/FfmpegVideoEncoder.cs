using System.Diagnostics;
using StepsRecorder.Core.Settings;

namespace StepsRecorder.Core.Encoding;

public sealed class FfmpegVideoEncoder : IVideoEncoder
{
    private readonly AppSettings _settings;
    private Process? _process;
    private BinaryWriter? _stdin;

    public string EncoderName { get; }

    public FfmpegVideoEncoder(AppSettings settings, string encoderName)
    {
        _settings = settings;
        EncoderName = encoderName;
    }

    public void Start(string outputPath)
    {
        string ffmpegPath = EncoderDetector.ResolveFfmpegPath(_settings.Encoder.FfmpegPath);
        int w = _settings.Video.Width;
        int h = _settings.Video.Height;
        int fps = _settings.Video.Fps;

        // Hardware encoders may need additional flags
        string extraFlags = EncoderName switch
        {
            "h264_nvenc" => "-preset p4 -rc vbr -cq 28",
            "h264_amf"   => "-quality balanced",
            "h264_qsv"   => "-preset medium",
            _            => "-preset ultrafast -crf 28"
        };

        string args =
            $"-f rawvideo -pix_fmt bgra -s {w}x{h} -r {fps} -i pipe:0 " +
            $"-vcodec {EncoderName} -pix_fmt yuv420p {extraFlags} " +
            $"-movflags +faststart -y \"{outputPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false
        };

        _process = new Process { StartInfo = psi };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                System.Diagnostics.Debug.WriteLine($"[ffmpeg] {e.Data}");
        };

        _process.Start();
        _process.BeginErrorReadLine();

        // Use BaseStream for raw binary — StreamWriter would corrupt frames
        _stdin = new BinaryWriter(_process.StandardInput.BaseStream);
    }

    public void WriteFrame(ReadOnlySpan<byte> bgraFrame, long sessionMs)
    {
        if (_stdin is null) return;
        _stdin.Write(bgraFrame);
        // Do not flush every frame — let OS buffer handle it for throughput
    }

    public void Stop()
    {
        try
        {
            _stdin?.Flush();
            _stdin?.Close();
            _stdin = null;
            _process?.WaitForExit(15_000);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _process?.Dispose();
        GC.SuppressFinalize(this);
    }
}
