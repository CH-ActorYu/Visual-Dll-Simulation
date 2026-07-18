namespace Visual.Abstractions.Contracts;

public readonly record struct Point2D
{
    public Point2D(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Point coordinates must be finite.");
        }

        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }
}
