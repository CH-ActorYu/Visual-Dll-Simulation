using Visual.Abstractions.Contracts;

namespace Visual.Image.Contracts;

public static class PixelFormatExtensions
{
    public static int BytesPerPixel(this PixelFormat format) => format switch
    {
        PixelFormat.Gray8 => 1,
        PixelFormat.Bgr24 or PixelFormat.Rgb24 => 3,
        PixelFormat.Bgra32 => 4,
        _ => throw new VisionException(VisionErrorCode.InvalidInput, $"Unsupported pixel format: {format}.")
    };
}
