using Visual.Abstractions.Contracts;

namespace Visual.AppCore.Interaction;

public sealed class RoiInteractionController
{
    public const int MinimumSize = 16;
    private Point2D? _start;

    public RoiRect? CurrentRoi { get; private set; }

    public void Begin(Point2D viewportPoint) => _start = viewportPoint;

    public RoiRect? Complete(Point2D viewportPoint, ViewportTransform transform)
    {
        if (_start is not { } start || transform.Width <= 0 || transform.Height <= 0)
        {
            _start = null;
            return null;
        }

        _start = null;
        var first = transform.ToImage(start);
        var second = transform.ToImage(viewportPoint);
        var left = Math.Clamp((int)Math.Floor(Math.Min(first.X, second.X)), 0, transform.ImageSize.Width - 1);
        var top = Math.Clamp((int)Math.Floor(Math.Min(first.Y, second.Y)), 0, transform.ImageSize.Height - 1);
        var right = Math.Clamp((int)Math.Ceiling(Math.Max(first.X, second.X)), left + 1, transform.ImageSize.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(Math.Max(first.Y, second.Y)), top + 1, transform.ImageSize.Height);

        if (right - left < MinimumSize || bottom - top < MinimumSize)
        {
            return null;
        }

        CurrentRoi = new RoiRect(left, top, right - left, bottom - top);
        return CurrentRoi;
    }

    public void Restore(RoiRect? roi) => CurrentRoi = roi;

    public RoiRect? ClampTo(ImageSize imageSize)
    {
        if (CurrentRoi is not { } roi)
        {
            return null;
        }

        try
        {
            var clamped = roi.ClampTo(imageSize);
            if (clamped.Width < MinimumSize || clamped.Height < MinimumSize)
            {
                CurrentRoi = null;
                return null;
            }

            CurrentRoi = clamped;
            return clamped;
        }
        catch (VisionException exception) when (exception.ErrorCode == VisionErrorCode.InvalidInput)
        {
            CurrentRoi = null;
            return null;
        }
    }

    public void Clear()
    {
        _start = null;
        CurrentRoi = null;
    }
}
