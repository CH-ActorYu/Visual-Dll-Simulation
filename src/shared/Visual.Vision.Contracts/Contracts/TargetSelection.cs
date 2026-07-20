using System.Collections.ObjectModel;
using Visual.Abstractions.Contracts;

namespace Visual.Vision.Contracts;

public sealed record TargetSelection
{
    public TargetSelection(IEnumerable<Point2D> polygon)
    {
        var snapshot = (polygon ?? throw Invalid("A target selection polygon is required.")).ToArray();
        ValidatePolygon(snapshot);
        Polygon = new ReadOnlyCollection<Point2D>(snapshot);
        Bounds = ComputeBounds(snapshot);
    }

    public IReadOnlyList<Point2D> Polygon { get; }

    public RoiRect Bounds { get; }

    public static TargetSelection FromBounds(RoiRect bounds) => new(
    [
        new Point2D(bounds.X, bounds.Y),
        new Point2D((double)bounds.X + bounds.Width, bounds.Y),
        new Point2D((double)bounds.X + bounds.Width, (double)bounds.Y + bounds.Height),
        new Point2D(bounds.X, (double)bounds.Y + bounds.Height)
    ]);

    private static void ValidatePolygon(IReadOnlyList<Point2D> polygon)
    {
        if (polygon.Count < 3 || polygon.Distinct().Count() < 3)
        {
            throw Invalid("A target selection requires at least three distinct points.");
        }

        if (polygon.Any(point => point.X < 0 || point.Y < 0))
        {
            throw Invalid("Target selection coordinates cannot be negative.");
        }

        var doubledArea = 0d;
        for (var index = 0; index < polygon.Count; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Count];
            doubledArea += current.X * next.Y - next.X * current.Y;
        }

        if (!double.IsFinite(doubledArea) || Math.Abs(doubledArea) <= double.Epsilon)
        {
            throw Invalid("Target selection points must enclose a non-zero area.");
        }
    }

    private static RoiRect ComputeBounds(IReadOnlyList<Point2D> polygon)
    {
        var left = Math.Floor(polygon.Min(point => point.X));
        var top = Math.Floor(polygon.Min(point => point.Y));
        var right = Math.Ceiling(polygon.Max(point => point.X));
        var bottom = Math.Ceiling(polygon.Max(point => point.Y));

        if (left < 0 || top < 0 || right > int.MaxValue || bottom > int.MaxValue ||
            right <= left || bottom <= top)
        {
            throw Invalid("Target selection bounds are outside the supported pixel range.");
        }

        return new RoiRect(
            checked((int)left),
            checked((int)top),
            checked((int)(right - left)),
            checked((int)(bottom - top)));
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
