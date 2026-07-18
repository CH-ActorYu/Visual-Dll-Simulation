using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Image.Internal;

internal static class FrameLayout
{
    public static int GetMinimumStride(ImageSize size, PixelFormat format)
    {
        try
        {
            return checked(size.Width * format.BytesPerPixel());
        }
        catch (OverflowException exception)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame dimensions exceed the supported row size.", exception);
        }
    }

    public static int GetRequiredLength(ImageSize size, PixelFormat format, int stride)
    {
        try
        {
            var rowBytes = GetMinimumStride(size, format);
            if (stride < rowBytes)
            {
                throw new VisionException(
                    VisionErrorCode.InvalidInput,
                    $"Stride {stride} is smaller than the required row size {rowBytes}.");
            }

            return checked(stride * size.Height);
        }
        catch (OverflowException exception)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame dimensions exceed the supported buffer size.", exception);
        }
    }
}
