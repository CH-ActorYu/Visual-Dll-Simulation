using Visual.Abstractions.Contracts;
using Visual.Image.Internal;

namespace Visual.Image.Contracts;

public sealed class ImageFrame
{
    public ImageFrame(
        FrameInfo info,
        PixelFormat format,
        int stride,
        ReadOnlyMemory<byte> data)
    {
        Info = info ?? throw new VisionException(VisionErrorCode.InvalidInput, "Frame metadata is required.");

        var requiredLength = FrameLayout.GetRequiredLength(info.Size, format, stride);
        if (data.Length < requiredLength)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"Frame data length {data.Length} is smaller than the required length {requiredLength}.");
        }

        Format = format;
        Stride = stride;
        Data = data[..requiredLength];
    }

    public FrameInfo Info { get; }

    public PixelFormat Format { get; }

    public int Stride { get; }

    public ReadOnlyMemory<byte> Data { get; }
}
