using Visual.Abstractions.Contracts;

namespace Visual.AppCore.Interaction;

public readonly record struct ViewportTransform(double Width, double Height, ImageSize ImageSize)
{
    public double Scale => Math.Min(Width / ImageSize.Width, Height / ImageSize.Height);

    public double DisplayWidth => ImageSize.Width * Scale;

    public double DisplayHeight => ImageSize.Height * Scale;

    public double OffsetX => (Width - DisplayWidth) / 2;

    public double OffsetY => (Height - DisplayHeight) / 2;

    public Point2D ToImage(Point2D point) => new(
        Math.Clamp((point.X - OffsetX) / Scale, 0, ImageSize.Width),
        Math.Clamp((point.Y - OffsetY) / Scale, 0, ImageSize.Height));

    public Point2D ToViewport(Point2D point) => new(
        OffsetX + point.X * Scale,
        OffsetY + point.Y * Scale);
}
