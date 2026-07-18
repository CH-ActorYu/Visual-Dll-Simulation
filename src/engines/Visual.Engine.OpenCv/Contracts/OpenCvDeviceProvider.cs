using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.IO.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvDeviceProvider(int maximumDevices = 8) : ICameraDeviceProvider
{
    public int MaximumDevices { get; } = maximumDevices >= 0
        ? maximumDevices
        : throw OpenCvErrors.Invalid("Maximum camera count cannot be negative.");

    public ValueTask<IReadOnlyList<CameraDescriptor>> EnumerateAsync(
        CancellationToken cancellationToken = default)
    {
        var cameras = new List<CameraDescriptor>();
        for (var index = 0; index < MaximumDevices; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
                if (!capture.IsOpened())
                {
                    continue;
                }

                cameras.Add(CreateDescriptor(index, capture));
            }
            catch
            {
                // A failing optional device must not hide other available cameras.
            }
        }

        return ValueTask.FromResult<IReadOnlyList<CameraDescriptor>>(cameras);
    }

    public ValueTask<ICameraDevice> OpenAsync(
        CameraDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();
        var index = ParseIndex(descriptor.Id);
        return ValueTask.FromResult<ICameraDevice>(new OpenCvCameraDevice(index, descriptor));
    }

    private static CameraDescriptor CreateDescriptor(int index, VideoCapture capture)
    {
        var width = Math.Max(1, checked((int)capture.FrameWidth));
        var height = Math.Max(1, checked((int)capture.FrameHeight));
        var fps = capture.Fps;
        if (!double.IsFinite(fps) || fps <= 0)
        {
            fps = 30;
        }

        var capabilities = new CameraCapabilities([new ImageSize(width, height)], [fps]);
        return new CameraDescriptor(
            $"opencv-camera:{index}",
            $"OpenCV Camera {index}",
            null,
            null,
            capabilities);
    }

    private static int ParseIndex(string id)
    {
        const string prefix = "opencv-camera:";
        return id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(id[prefix.Length..], out var index) && index >= 0
            ? index
            : throw OpenCvErrors.Invalid($"Camera descriptor ID '{id}' is not an OpenCV camera ID.");
    }
}

internal sealed class OpenCvCameraDevice(int index, CameraDescriptor descriptor) : CameraDeviceBase(descriptor)
{
    private VideoCapture? _capture;

    protected override ValueTask OnConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            throw OpenCvErrors.Device($"Camera {index} is unavailable.");
        }

        _capture = capture;
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnDisconnectAsync(CancellationToken cancellationToken)
    {
        _capture?.Dispose();
        _capture = null;
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnSetParameterAsync(
        CameraParameter parameter,
        CameraParameterValue value,
        CancellationToken cancellationToken)
    {
        var capture = _capture ?? throw new VisionException(VisionErrorCode.NotRunning, "Camera is not connected.");
        var property = parameter switch
        {
            CameraParameter.Exposure => VideoCaptureProperties.Exposure,
            CameraParameter.Gain => VideoCaptureProperties.Gain,
            CameraParameter.Focus => VideoCaptureProperties.Focus,
            _ => throw OpenCvErrors.Invalid($"Unsupported camera parameter {parameter}.")
        };
        var applied = capture.Set(property, value.Automatic ? -1 : value.Value);
        if (!applied)
        {
            throw OpenCvErrors.Device($"Camera rejected parameter {parameter}.");
        }

        return ValueTask.CompletedTask;
    }
}
