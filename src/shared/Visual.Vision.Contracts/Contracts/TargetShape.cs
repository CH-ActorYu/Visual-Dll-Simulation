using System.Collections.ObjectModel;
using Visual.Abstractions.Contracts;

namespace Visual.Vision.Contracts;

public sealed record TargetShape
{
    public TargetShape(
        IEnumerable<Point2D> contour,
        RoiRect bounds,
        Point2D center,
        PixelMeasureAxis measureAxis,
        double measureAxisPixelSize)
    {
        var snapshot = (contour ?? throw Invalid("A target contour is required.")).ToArray();
        if (snapshot.Length < 3 || snapshot.Distinct().Count() < 3)
        {
            throw Invalid("A target contour requires at least three distinct points.");
        }

        if (bounds.X < 0 || bounds.Y < 0)
        {
            throw Invalid("Target shape bounds cannot start at a negative coordinate.");
        }

        if (snapshot.Any(point => !IsInsideOrOnBoundary(bounds, point)))
        {
            throw Invalid("Every target contour point must be inside its bounds.");
        }

        if (!EnclosesArea(snapshot))
        {
            throw Invalid("Target contour points must enclose a non-zero area.");
        }

        if (!IsInsideOrOnBoundary(bounds, center))
        {
            throw Invalid("The target center must be inside its bounds.");
        }

        if (!double.IsFinite(measureAxisPixelSize) || measureAxisPixelSize <= 0)
        {
            throw Invalid("The target measurement-axis pixel size must be positive and finite.");
        }

        Contour = new ReadOnlyCollection<Point2D>(snapshot);
        Bounds = bounds;
        Center = center;
        MeasureAxis = measureAxis;
        MeasureAxisPixelSize = measureAxisPixelSize;
    }

    public IReadOnlyList<Point2D> Contour { get; }

    public RoiRect Bounds { get; }

    public Point2D Center { get; }

    public PixelMeasureAxis MeasureAxis { get; }

    public double MeasureAxisPixelSize { get; }

    private static bool IsInsideOrOnBoundary(RoiRect bounds, Point2D point) =>
        point.X >= bounds.X && point.X <= (long)bounds.X + bounds.Width &&
        point.Y >= bounds.Y && point.Y <= (long)bounds.Y + bounds.Height;

    private static bool EnclosesArea(IReadOnlyList<Point2D> contour)
    {
        var doubledArea = 0d;
        for (var index = 0; index < contour.Count; index++)
        {
            var current = contour[index];
            var next = contour[(index + 1) % contour.Count];
            doubledArea += current.X * next.Y - next.X * current.Y;
        }

        return double.IsFinite(doubledArea) && Math.Abs(doubledArea) > double.Epsilon;
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
