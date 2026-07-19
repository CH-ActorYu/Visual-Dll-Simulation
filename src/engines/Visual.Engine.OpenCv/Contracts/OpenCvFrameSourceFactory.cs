using System.Globalization;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.IO.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvFrameSourceFactory : IFrameSourceFactory
{
    public IFrameSource Create(FrameSourceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var capacity = ReadInt(descriptor, "BufferCapacity", 3, minimum: 1);
        return descriptor.Kind switch
        {
            FrameSourceKind.Camera => new OpenCvCameraSource(
                descriptor.SourceId,
                ReadCameraIndex(descriptor),
                capacity),
            FrameSourceKind.VideoFile => new OpenCvVideoFileSource(
                descriptor.SourceId,
                descriptor.Location!,
                capacity,
                ReadBoolean(descriptor, "LoopPlayback", false)),
            FrameSourceKind.ImageFolder => new OpenCvImageFolderSource(
                descriptor.SourceId,
                descriptor.Location!,
                ReadDouble(descriptor, "Fps", 10),
                capacity),
            _ => throw OpenCvErrors.Invalid($"Unsupported frame source kind {descriptor.Kind}.")
        };
    }

    private static int ReadCameraIndex(FrameSourceDescriptor descriptor)
    {
        var text = descriptor.Location;
        if (string.IsNullOrWhiteSpace(text) && descriptor.Options.TryGetValue("DeviceIndex", out var option))
        {
            text = option;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : throw OpenCvErrors.Invalid("Camera device index is invalid.");
    }

    private static int ReadInt(FrameSourceDescriptor descriptor, string key, int fallback, int minimum)
    {
        if (!descriptor.Options.TryGetValue(key, out var text))
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum
            ? value
            : throw OpenCvErrors.Invalid($"Frame source option {key} is invalid.");
    }

    private static double ReadDouble(FrameSourceDescriptor descriptor, string key, double fallback)
    {
        if (!descriptor.Options.TryGetValue(key, out var text))
        {
            return fallback;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
               double.IsFinite(value) && value > 0
               ? value
               : throw OpenCvErrors.Invalid($"Frame source option {key} is invalid.");
    }

    private static bool ReadBoolean(FrameSourceDescriptor descriptor, string key, bool fallback)
    {
        if (!descriptor.Options.TryGetValue(key, out var text))
        {
            return fallback;
        }

        return bool.TryParse(text, out var value)
            ? value
            : throw OpenCvErrors.Invalid($"Frame source option {key} is invalid.");
    }
}
