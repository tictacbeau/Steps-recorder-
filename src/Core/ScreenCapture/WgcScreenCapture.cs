using System.Runtime.InteropServices;
using WinRT;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace StepsRecorder.Core.ScreenCapture;

/// <summary>
/// Captures frames from the primary monitor using Windows.Graphics.Capture.
/// Requires Windows 10 1803+ (build 17134) and a DirectX 11 GPU.
/// The system will show a yellow border around the captured display — this is a Windows security
/// feature and cannot be suppressed by third-party applications.
/// </summary>
public sealed class WgcScreenCapture : IFrameSource
{
    private readonly int _width;
    private readonly int _height;

    private ID3D11Device? _d3d11Device;
    private ID3D11DeviceContext? _d3d11Context;
    private IDirect3DDevice? _winrtDevice;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private ID3D11Texture2D? _stagingTexture;

    private readonly SemaphoreSlim _frameReady = new(0, 1);
    private Direct3D11CaptureFrame? _pendingFrame;
    private readonly object _frameLock = new();
    private volatile bool _stopped;

    public int Width => _width;
    public int Height => _height;

    public WgcScreenCapture(GraphicsCaptureItem item)
    {
        _width = item.Size.Width;
        _height = item.Size.Height;
        Initialize(item);
    }

    private void Initialize(GraphicsCaptureItem item)
    {
        // D3D11 device with BGRA support (mandatory for WGC)
        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            [FeatureLevel.Level_11_0],
            out _d3d11Device,
            out _d3d11Context);

        // Bridge: Vortice ID3D11Device → WinRT IDirect3DDevice
        _winrtDevice = CreateWinRtDevice(_d3d11Device!);

        // Staging texture for CPU readback
        var stagingDesc = new Texture2DDescription
        {
            Width = _width,
            Height = _height,
            Format = Format.B8G8R8A8_UNorm,
            ArraySize = 1,
            MipLevels = 1,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read
        };
        _stagingTexture = _d3d11Device!.CreateTexture2D(stagingDesc);

        // Frame pool
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            new SizeInt32 { Width = _width, Height = _height });

        _framePool.FrameArrived += OnFrameArrived;

        _session = _framePool.CreateCaptureSession(item);
        _session.IsCursorCaptureEnabled = false;
        _session.StartCapture();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        var frame = sender.TryGetNextFrame();
        if (frame is null) return;

        lock (_frameLock)
        {
            _pendingFrame?.Dispose();
            _pendingFrame = frame;
        }

        try { _frameReady.Release(); }
        catch (SemaphoreFullException) { }
    }

    public bool TryGetNextFrame(Span<byte> buffer, out long captureTimeMs)
    {
        captureTimeMs = 0;
        if (_stopped) return false;

        if (!_frameReady.Wait(100)) return true;

        Direct3D11CaptureFrame? frame;
        lock (_frameLock)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
        }

        if (frame is null) return !_stopped;

        using (frame)
        {
            captureTimeMs = frame.SystemRelativeTime.Ticks / TimeSpan.TicksPerMillisecond;
            CopyFrameToBuffer(frame, buffer);
        }

        return true;
    }

    private unsafe void CopyFrameToBuffer(Direct3D11CaptureFrame frame, Span<byte> buffer)
    {
        // Extract the underlying D3D11 texture via IDirect3DDxgiInterfaceAccess
        var dxgiAccess = (IDirect3DDxgiInterfaceAccess)frame.Surface;
        var textureGuid = typeof(ID3D11Texture2D).GUID;
        dxgiAccess.GetInterface(ref textureGuid, out object textureObj);

        using var srcTexture = (ID3D11Texture2D)textureObj;

        // GPU → staging copy
        _d3d11Context!.CopyResource(_stagingTexture!, srcTexture);

        // Map and copy rows — MUST use RowPitch (not Width*4) to handle GPU alignment
        var mapped = _d3d11Context.Map(_stagingTexture!, 0, MapType.Read, MapFlags.None);
        try
        {
            int rowBytes = _width * 4;
            for (int row = 0; row < _height; row++)
            {
                var src = new Span<byte>((byte*)mapped.DataPointer + (long)row * mapped.RowPitch, rowBytes);
                src.CopyTo(buffer.Slice(row * rowBytes, rowBytes));
            }
        }
        finally
        {
            _d3d11Context.Unmap(_stagingTexture!, 0);
        }
    }

    public void StopCapture()
    {
        _stopped = true;
        try { _frameReady.Release(); } catch { }
        _session?.Dispose();
        _framePool?.Dispose();
    }

    public void Dispose()
    {
        StopCapture();
        lock (_frameLock) { _pendingFrame?.Dispose(); _pendingFrame = null; }
        _stagingTexture?.Dispose();
        _d3d11Context?.Dispose();
        _d3d11Device?.Dispose();
        _frameReady.Dispose();
        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------
    // Factory: create GraphicsCaptureItem for the primary monitor
    // -------------------------------------------------------------------

    public static GraphicsCaptureItem CreateForPrimaryMonitor()
    {
        IntPtr hMonitor = MonitorFromPoint(new NATIVE_POINT { x = 0, y = 0 }, 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        return CreateForMonitor(hMonitor);
    }

    private static GraphicsCaptureItem CreateForMonitor(IntPtr hMonitor)
    {
        // Get the WinRT activation factory for GraphicsCaptureItem, which also
        // implements IGraphicsCaptureItemInterop for Win32 interop
        string runtimeClass = "Windows.Graphics.Capture.GraphicsCaptureItem";
        int hrStr = WindowsCreateString(runtimeClass, (uint)runtimeClass.Length, out nint hstring);
        Marshal.ThrowExceptionForHR(hrStr);

        try
        {
            var interopGuid = typeof(IGraphicsCaptureItemInterop).GUID;
            int hrFactory = RoGetActivationFactory(hstring, ref interopGuid, out nint factoryPtr);
            Marshal.ThrowExceptionForHR(hrFactory);

            try
            {
                var factory = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);

                // IGraphicsCaptureItemInterop::CreateForMonitor returns a native IUnknown*
                // that wraps the GraphicsCaptureItem WinRT object
                var itemGuid = typeof(GraphicsCaptureItem).GUID;
                nint itemPtr = factory.CreateForMonitor(hMonitor, ref itemGuid);

                // GraphicsCaptureItem is a WinRT runtime class — FromAbi is generated by CsWinRT
                var item = GraphicsCaptureItem.FromAbi(itemPtr);
                // FromAbi takes ownership (does not AddRef again), so do NOT Release
                return item;
            }
            finally
            {
                Marshal.Release(factoryPtr);
            }
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    // -------------------------------------------------------------------
    // Vortice ID3D11Device → WinRT IDirect3DDevice bridge
    // -------------------------------------------------------------------

    private static IDirect3DDevice CreateWinRtDevice(ID3D11Device d3d11Device)
    {
        using var dxgiDevice = d3d11Device.QueryInterface<IDXGIDevice>();

        int hr = D3D11CreateDirect3DDeviceFromDXGIDevice(dxgiDevice.NativePointer, out nint devicePtr);
        Marshal.ThrowExceptionForHR(hr);

        try
        {
            // IDirect3DDevice is a WinRT interface (not class), so use MarshalInspectable
            return WinRT.MarshalInspectable<IDirect3DDevice>.FromAbi(devicePtr);
        }
        finally
        {
            Marshal.Release(devicePtr); // MarshalInspectable.FromAbi AddRef's, balance here
        }
    }

    // -------------------------------------------------------------------
    // P/Invoke declarations
    // -------------------------------------------------------------------

    [DllImport("d3d11.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int D3D11CreateDirect3DDeviceFromDXGIDevice(
        nint dxgiDevice, out nint graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        nint activatableClassId, ref Guid iid, out nint factory);

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int WindowsCreateString(string sourceString, uint length, out nint hstring);

    [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WindowsDeleteString(nint hstring);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr MonitorFromPoint(NATIVE_POINT pt, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NATIVE_POINT { public int x, y; }

    // -------------------------------------------------------------------
    // COM interop interfaces
    // -------------------------------------------------------------------

    // IDirect3DDxgiInterfaceAccess - extracts DXGI/D3D interfaces from a WinRT surface
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        void GetInterface([In] ref Guid iid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppObj);
    }

    // IGraphicsCaptureItemInterop - creates GraphicsCaptureItem from Win32 handles
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(IntPtr window, [In] ref Guid iid);
        nint CreateForMonitor(IntPtr monitor, [In] ref Guid iid);
    }
}
