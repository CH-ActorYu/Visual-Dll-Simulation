using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvTracker : ITracker
{
    private readonly object _sync = new();
    private readonly OpenCvImageAdapter _adapter = new();
    private Mat? _template;
    private RoiRect? _lastRegion;
    private readonly int _redetectInterval;
    private int _framesSinceInitialization;
    private int _isDisposed;

    public OpenCvTracker(int redetectInterval = 10)
    {
        _redetectInterval = redetectInterval > 0
            ? redetectInterval
            : throw OpenCvErrors.Invalid("Tracker redetection interval must be positive.");
    }

    public void Initialize(ImageFrame frame, RoiRect roi)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        ArgumentNullException.ThrowIfNull(frame);
        var region = roi.ClampTo(frame.Info.Size);
        try
        {
            using var source = _adapter.ToMat(frame);
            using var view = new Mat(source.Mat, ToRect(region));
            var template = view.Clone();
            lock (_sync)
            {
                _template?.Dispose();
                _template = template;
                _lastRegion = region;
                _framesSinceInitialization = 0;
            }
        }
        catch (Exception exception)
        {
            throw OpenCvErrors.Normalize(exception, "OpenCV tracker initialization failed.");
        }
    }

    public TargetCandidate? Update(ImageFrame frame)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        ArgumentNullException.ThrowIfNull(frame);
        lock (_sync)
        {
            if (_template is null || _lastRegion is null)
            {
                return null;
            }

            if (++_framesSinceInitialization >= _redetectInterval)
            {
                return null;
            }

            try
            {
                var search = Expand(_lastRegion.Value, frame.Info.Size);
                if (search.Width < _template.Width || search.Height < _template.Height)
                {
                    return null;
                }

                using var source = _adapter.ToMat(frame);
                using var searchView = new Mat(source.Mat, ToRect(search));
                using var result = new Mat();
                Cv2.MatchTemplate(searchView, _template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out var confidence, out _, out var location);
                if (!double.IsFinite(confidence) || confidence < 0.5)
                {
                    return null;
                }

                var box = new RoiRect(
                    search.X + location.X,
                    search.Y + location.Y,
                    _template.Width,
                    _template.Height);
                _lastRegion = box;
                return new TargetCandidate(
                    box,
                    new Point2D(box.X + box.Width / 2d, box.Y + box.Height / 2d),
                    box.Width,
                    Math.Clamp(confidence, 0, 1));
            }
            catch (Exception exception)
            {
                throw OpenCvErrors.Normalize(exception, "OpenCV tracking failed.");
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            lock (_sync)
            {
                _template?.Dispose();
                _template = null;
                _lastRegion = null;
                _framesSinceInitialization = 0;
            }
        }
    }

    private static RoiRect Expand(RoiRect region, ImageSize size)
    {
        var horizontal = Math.Max(region.Width, 8);
        var vertical = Math.Max(region.Height, 8);
        return new RoiRect(
            region.X - horizontal,
            region.Y - vertical,
            checked(region.Width + horizontal * 2),
            checked(region.Height + vertical * 2)).ClampTo(size);
    }

    private static Rect ToRect(RoiRect region) => new(region.X, region.Y, region.Width, region.Height);
}
