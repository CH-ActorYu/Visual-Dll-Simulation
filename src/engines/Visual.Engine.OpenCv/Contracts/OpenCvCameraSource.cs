using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvCameraSource : OpenCvFrameSourceBase
{
    private readonly int _deviceIndex;
    private VideoCapture? _capture;

    public OpenCvCameraSource(string sourceId, int deviceIndex = 0, int bufferCapacity = 3)
        : base(sourceId, bufferCapacity)
    {
        _deviceIndex = deviceIndex >= 0
            ? deviceIndex
            : throw OpenCvErrors.Invalid("Camera device index cannot be negative.");
    }

    protected override ValueTask OnOpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var capture = new VideoCapture(_deviceIndex, VideoCaptureAPIs.DSHOW);
            if (!capture.IsOpened())
            {
                capture.Dispose();
                throw OpenCvErrors.Device($"Camera {_deviceIndex} is unavailable.");
            }

            _capture = capture;
            Width = checked((int)capture.FrameWidth);
            Height = checked((int)capture.FrameHeight);
            Fps = NormalizeFps(capture.Fps);
            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            _capture?.Dispose();
            _capture = null;
            throw exception is VisionException ? exception : OpenCvErrors.Device("Camera open failed.", exception);
        }
    }

    protected override async Task RunCaptureAsync(CancellationToken cancellationToken)
    {
        var capture = _capture ?? throw OpenCvErrors.Device("Camera source is not open.");
        using var frame = new Mat();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!capture.Read(frame) || frame.Empty())
            {
                throw OpenCvErrors.Device($"Camera {_deviceIndex} stopped producing frames.");
            }

            Publish(frame);
            await Task.Yield();
        }
    }

    protected override ValueTask OnCloseAsync(CancellationToken cancellationToken)
    {
        _capture?.Dispose();
        _capture = null;
        return ValueTask.CompletedTask;
    }
}
