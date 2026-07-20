using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Internal;

internal static class OpenCvTargetGeometry
{
    public static Point[] Simplify(Point[] contour)
    {
        var epsilon = Math.Max(1, Cv2.ArcLength(contour, true) * 0.01);
        var simplified = Cv2.ApproxPolyDP(contour, epsilon, true);
        return simplified.Length >= 3 ? simplified : contour;
    }

    public static TargetShape CreateShape(
        IReadOnlyList<Point> contour,
        int offsetX,
        int offsetY,
        PixelMeasureAxis measureAxis)
    {
        if (contour.Count < 3)
        {
            throw OpenCvErrors.Invalid("A tracked target contour requires at least three points.");
        }

        var original = contour.ToArray();
        var simplified = Simplify(original);
        var localBounds = Cv2.BoundingRect(original);
        var bounds = new RoiRect(
            checked(offsetX + localBounds.X),
            checked(offsetY + localBounds.Y),
            localBounds.Width,
            localBounds.Height);
        var globalContour = simplified
            .Select(point => new Point2D(offsetX + point.X, offsetY + point.Y))
            .ToArray();
        var moments = Cv2.Moments(original);
        var center = Math.Abs(moments.M00) > double.Epsilon
            ? new Point2D(offsetX + moments.M10 / moments.M00, offsetY + moments.M01 / moments.M00)
            : new Point2D(bounds.X + bounds.Width / 2d, bounds.Y + bounds.Height / 2d);
        var rotated = Cv2.MinAreaRect(original);
        var pixelSize = measureAxis switch
        {
            PixelMeasureAxis.Horizontal => localBounds.Width,
            PixelMeasureAxis.Vertical => localBounds.Height,
            PixelMeasureAxis.MajorAxis => Math.Max(rotated.Size.Width, rotated.Size.Height),
            PixelMeasureAxis.MinorAxis => Math.Min(rotated.Size.Width, rotated.Size.Height),
            _ => throw OpenCvErrors.Invalid($"Unsupported measure axis {measureAxis}.")
        };

        return new TargetShape(globalContour, bounds, center, measureAxis, pixelSize);
    }
}
