using System.Text.Json.Serialization;

namespace Visual.Abstractions.Contracts;

public readonly record struct RoiRect
{
    [JsonConstructor]
    public RoiRect(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"ROI dimensions must be positive. Received {width}x{height}.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public long Area => (long)Width * Height;

    public bool Contains(Point2D point) =>
        point.X >= X && point.X < (long)X + Width &&
        point.Y >= Y && point.Y < (long)Y + Height;

    public RoiRect ClampTo(int width, int height)
    {
        _ = new ImageSize(width, height);

        var left = Math.Max((long)X, 0);
        var top = Math.Max((long)Y, 0);
        var right = Math.Min((long)X + Width, width);
        var bottom = Math.Min((long)Y + Height, height);

        if (right <= left || bottom <= top)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"ROI ({X}, {Y}, {Width}, {Height}) is outside image bounds {width}x{height}.");
        }

        return new RoiRect(
            checked((int)left),
            checked((int)top),
            checked((int)(right - left)),
            checked((int)(bottom - top)));
    }

    public RoiRect ClampTo(ImageSize size) => ClampTo(size.Width, size.Height);
}
