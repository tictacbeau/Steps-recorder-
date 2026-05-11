namespace StepsRecorder.Core.ScreenCapture;

public interface IFrameSource : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Blocks until a new frame is available or capture stops.
    /// Returns false if no more frames will arrive.
    /// </summary>
    bool TryGetNextFrame(Span<byte> buffer, out long captureTimeMs);

    void StopCapture();
}
