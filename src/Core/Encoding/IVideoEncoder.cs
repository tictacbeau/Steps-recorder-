namespace StepsRecorder.Core.Encoding;

public interface IVideoEncoder : IDisposable
{
    string EncoderName { get; }
    void Start(string outputPath);
    void WriteFrame(ReadOnlySpan<byte> bgraFrame, long sessionMs);
    void Stop();
}
