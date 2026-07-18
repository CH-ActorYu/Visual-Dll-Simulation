using Visual.Abstractions.Contracts;

namespace Visual.VisionBase.Contracts;

public static class GeometryUtils
{
    public static Point2D CenterOf(RoiRect rectangle) => new(
        rectangle.X + rectangle.Width / 2d,
        rectangle.Y + rectangle.Height / 2d);

    public static double PixelToPhysical(double pixels, double physicalReference, double pixelReference)
    {
        if (!double.IsFinite(pixels) || pixels < 0 ||
            !double.IsFinite(physicalReference) || physicalReference <= 0 ||
            !double.IsFinite(pixelReference) || pixelReference <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Pixel-to-physical conversion inputs are invalid.");
        }

        return pixels * physicalReference / pixelReference;
    }
}
