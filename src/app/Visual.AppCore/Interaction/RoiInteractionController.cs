using Visual.Abstractions.Contracts;

namespace Visual.AppCore.Interaction;

public sealed class RoiInteractionController
{
    public const int MinimumSize = 16;
    private readonly object _sync = new();
    private Point2D? _start;
    private RoiRect? _currentRoi;

    public RoiRect? CurrentRoi
    {
        get
        {
            lock (_sync)
            {
                return _currentRoi;
            }
        }
    }

    public void Begin(Point2D viewportPoint)
    {
        lock (_sync)
        {
            _start = viewportPoint;
        }
    }

    public RoiRect? Complete(Point2D viewportPoint, ViewportTransform transform)
    {
        Point2D? start;
        lock (_sync)
        {
            start = _start;
            _start = null;
        }

        if (start is not { } firstPoint || transform.Width <= 0 || transform.Height <= 0)
        {
            return null;
        }

        var first = transform.ToImage(firstPoint);
        var second = transform.ToImage(viewportPoint);
        var left = Math.Clamp((int)Math.Floor(Math.Min(first.X, second.X)), 0, transform.ImageSize.Width - 1);
        var top = Math.Clamp((int)Math.Floor(Math.Min(first.Y, second.Y)), 0, transform.ImageSize.Height - 1);
        var right = Math.Clamp((int)Math.Ceiling(Math.Max(first.X, second.X)), left + 1, transform.ImageSize.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(Math.Max(first.Y, second.Y)), top + 1, transform.ImageSize.Height);

        if (right - left < MinimumSize || bottom - top < MinimumSize)
        {
            return null;
        }

        var accepted = new RoiRect(left, top, right - left, bottom - top);
        lock (_sync)
        {
            _currentRoi = accepted;
        }

        return accepted;
    }

    public void Restore(RoiRect? roi)
    {
        lock (_sync)
        {
            _currentRoi = roi;
        }
    }

    public RoiRect? ClampTo(ImageSize imageSize)
    {
        RoiRect? current;
        lock (_sync)
        {
            current = _currentRoi;
        }

        if (current is not { } roi)
        {
            return null;
        }

        try
        {
            var clamped = roi.ClampTo(imageSize);
            if (clamped.Width < MinimumSize || clamped.Height < MinimumSize)
            {
                lock (_sync)
                {
                    _currentRoi = null;
                }
                return null;
            }

            lock (_sync)
            {
                _currentRoi = clamped;
            }
            return clamped;
        }
        catch (VisionException exception) when (exception.ErrorCode == VisionErrorCode.InvalidInput)
        {
            lock (_sync)
            {
                _currentRoi = null;
            }
            return null;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _start = null;
            _currentRoi = null;
        }
    }
}
