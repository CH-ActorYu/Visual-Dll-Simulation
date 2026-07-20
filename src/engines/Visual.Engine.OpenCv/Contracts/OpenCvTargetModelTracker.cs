using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvTargetModelTracker : ITargetModelTracker
{
    private const int ReacquireAfterFailures = 3;
    private const int NotFoundAfterFailures = 9;
    private readonly DetectionProfile _profile;
    private readonly OpenCvImageAdapter _adapter = new();
    private Mat? _referenceContour;
    private double _referenceArea;
    private double[]? _referenceHistogram;
    private TargetShape? _lastShape;
    private RoiRect? _initialSearchRegion;
    private int _consecutiveFailures;
    private bool _disposed;

    public OpenCvTargetModelTracker(DetectionProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public void Initialize(ImageFrame frame, ITargetModel model, RoiRect? searchRegion = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(model);
        if (model is not OpenCvTargetModel openCvModel)
        {
            throw OpenCvErrors.Invalid("The OpenCV tracker requires an OpenCV target model.");
        }

        if (!string.Equals(model.DetectionProfileId, _profile.ProfileId, StringComparison.Ordinal))
        {
            throw OpenCvErrors.Invalid("The target model and tracker must use the same detection profile.");
        }

        ValidateRegion(searchRegion, frame.Info.Size);
        EnsureShapeInsideFrame(model.ReferenceShape, frame.Info.Size);
        if (searchRegion is { } configuredRegion && !Contains(configuredRegion, model.ReferenceShape.Bounds))
        {
            throw OpenCvErrors.Invalid("The initial search region must contain the registered target.");
        }

        using var mask = openCvModel.Mask.Clone();
        using var template = openCvModel.Template.Clone();
        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);
        var reference = contours.OrderByDescending(contour => Cv2.ContourArea(contour)).FirstOrDefault();
        if (reference is null || reference.Length < 3)
        {
            throw OpenCvErrors.Invalid("The target model does not contain a usable contour.");
        }

        using var contourInput = InputArray.Create(reference);
        using var newContour = contourInput.GetMat().Clone();
        var newArea = Cv2.ContourArea(reference);
        var newHistogram = CreateHistogram(template, mask);

        _referenceContour?.Dispose();
        _referenceContour = newContour.Clone();
        _referenceArea = newArea;
        _referenceHistogram = newHistogram;
        _lastShape = model.ReferenceShape;
        _initialSearchRegion = searchRegion;
        _consecutiveFailures = 0;
    }

    public TargetTrackingResult Update(ImageFrame frame, RoiRect? searchRegion = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(frame);
        if (_referenceContour is null || _lastShape is null)
        {
            return new TargetTrackingResult(TargetTrackingStatus.Unregistered, null, 0);
        }

        if (_consecutiveFailures >= NotFoundAfterFailures)
        {
            return new TargetTrackingResult(TargetTrackingStatus.NotFound, null, 0);
        }

        var allowed = searchRegion ?? _initialSearchRegion ??
            new RoiRect(0, 0, frame.Info.Size.Width, frame.Info.Size.Height);
        ValidateRegion(allowed, frame.Info.Size);
        var useGlobalSearch = _consecutiveFailures >= ReacquireAfterFailures ||
            !Overlaps(_lastShape.Bounds, allowed);
        var activeRegion = useGlobalSearch
            ? allowed
            : Intersect(Expand(_lastShape.Bounds, allowed), allowed);

        try
        {
            using var full = _adapter.ToMat(frame);
            using var view = new Mat(full.Mat, ToRect(activeRegion));
            using var segmented = OpenCvSegmentation.Segment(view, frame.Format, _profile);
            using var gray = OpenCvSegmentation.ToGray(view, frame.Format);
            var match = FindBestMatch(segmented, gray, activeRegion, useGlobalSearch);
            if (match is not null && match.Value.Confidence >= _profile.MinimumConfidence)
            {
                _lastShape = OpenCvTargetGeometry.CreateShape(
                    match.Value.Contour,
                    activeRegion.X,
                    activeRegion.Y,
                    _profile.MeasureAxis);
                _consecutiveFailures = 0;
                return new TargetTrackingResult(
                    TargetTrackingStatus.Tracking,
                    _lastShape,
                    match.Value.Confidence);
            }

            return CreateLostResult(match?.Confidence ?? 0);
        }
        catch (Exception exception)
        {
            throw OpenCvErrors.Normalize(exception, "OpenCV target tracking failed.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _referenceContour?.Dispose();
        _referenceContour = null;
        _referenceArea = 0;
        _referenceHistogram = null;
        _lastShape = null;
    }

    private (Point[] Contour, double Confidence)? FindBestMatch(
        Mat segmented,
        Mat gray,
        RoiRect activeRegion,
        bool useGlobalSearch)
    {
        Cv2.FindContours(
            segmented,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        (Point[] Contour, double Confidence)? best = null;
        foreach (var contour in contours)
        {
            if (contour.Length < 3 || Cv2.ContourArea(contour) < 4)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            if (!useGlobalSearch &&
                (bounds.X == 0 || bounds.Y == 0 || bounds.Right == segmented.Width || bounds.Bottom == segmented.Height))
            {
                continue;
            }

            var areaRatio = Cv2.ContourArea(contour) / _referenceArea;
            if (!double.IsFinite(areaRatio) || areaRatio is < 0.25 or > 4)
            {
                continue;
            }

            var globalBounds = new RoiRect(
                activeRegion.X + bounds.X,
                activeRegion.Y + bounds.Y,
                bounds.Width,
                bounds.Height);
            if (!Contains(activeRegion, globalBounds))
            {
                continue;
            }

            using var candidate = InputArray.Create(contour);
            var distance = Cv2.MatchShapes(_referenceContour!, candidate, ShapeMatchModes.I1);
            if (!double.IsFinite(distance))
            {
                continue;
            }

            var shapeConfidence = 1d / (1d + 4d * distance);
            var scaleConfidence = Math.Exp(-0.2d * Math.Abs(Math.Log(areaRatio)));
            var appearanceConfidence = CompareAppearance(gray, contour, bounds);
            var confidence = Math.Clamp(
                shapeConfidence * scaleConfidence * (0.25d + 0.75d * appearanceConfidence),
                0,
                1);
            if (best is null || confidence > best.Value.Confidence)
            {
                best = (contour, confidence);
            }
        }

        return best;
    }

    private double CompareAppearance(Mat gray, Point[] contour, Rect bounds)
    {
        using var candidateMask = new Mat(bounds.Height, bounds.Width, MatType.CV_8UC1, Scalar.Black);
        var localContour = contour
            .Select(point => new Point(point.X - bounds.X, point.Y - bounds.Y))
            .ToArray();
        Cv2.DrawContours(candidateMask, [localContour], 0, Scalar.White, -1);
        using var candidateView = new Mat(gray, bounds);
        var candidateHistogram = CreateHistogram(candidateView, candidateMask);
        var referenceHistogram = _referenceHistogram!;
        var similarity = 0d;
        for (var index = 0; index < referenceHistogram.Length; index++)
        {
            var smoothedCandidate = candidateHistogram[index] * 0.6d;
            if (index > 0)
            {
                smoothedCandidate += candidateHistogram[index - 1] * 0.2d;
            }

            if (index + 1 < candidateHistogram.Length)
            {
                smoothedCandidate += candidateHistogram[index + 1] * 0.2d;
            }

            similarity += Math.Sqrt(referenceHistogram[index] * smoothedCandidate);
        }

        return Math.Clamp(similarity, 0, 1);
    }

    private static double[] CreateHistogram(Mat gray, Mat mask)
    {
        const int binCount = 8;
        var histogram = new double[binCount];
        var included = 0;
        var rows = gray.Rows;
        var columns = gray.Cols;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                if (mask.At<byte>(y, x) == 0)
                {
                    continue;
                }

                histogram[Math.Min(binCount - 1, gray.At<byte>(y, x) * binCount / 256)]++;
                included++;
            }
        }

        if (included == 0)
        {
            throw OpenCvErrors.Invalid("The target appearance mask is empty.");
        }

        for (var index = 0; index < histogram.Length; index++)
        {
            histogram[index] /= included;
        }

        return histogram;
    }

    private TargetTrackingResult CreateLostResult(double confidence)
    {
        _consecutiveFailures++;
        var status = _consecutiveFailures switch
        {
            < ReacquireAfterFailures => TargetTrackingStatus.TemporarilyLost,
            < NotFoundAfterFailures => TargetTrackingStatus.Reacquiring,
            _ => TargetTrackingStatus.NotFound
        };
        return new TargetTrackingResult(status, null, Math.Clamp(confidence, 0, 1));
    }

    private static RoiRect Expand(RoiRect bounds, RoiRect limit)
    {
        var left = Math.Max(limit.X, bounds.X - bounds.Width);
        var top = Math.Max(limit.Y, bounds.Y - bounds.Height);
        var right = Math.Min((long)limit.X + limit.Width, (long)bounds.X + bounds.Width * 2L);
        var bottom = Math.Min((long)limit.Y + limit.Height, (long)bounds.Y + bounds.Height * 2L);
        return new RoiRect(left, top, checked((int)(right - left)), checked((int)(bottom - top)));
    }

    private static RoiRect Intersect(RoiRect first, RoiRect second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min((long)first.X + first.Width, (long)second.X + second.Width);
        var bottom = Math.Min((long)first.Y + first.Height, (long)second.Y + second.Height);
        if (right <= left || bottom <= top)
        {
            throw OpenCvErrors.Invalid("The target is outside the configured search region.");
        }

        return new RoiRect(left, top, checked((int)(right - left)), checked((int)(bottom - top)));
    }

    private static bool Contains(RoiRect outer, RoiRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y &&
        (long)inner.X + inner.Width <= (long)outer.X + outer.Width &&
        (long)inner.Y + inner.Height <= (long)outer.Y + outer.Height;

    private static bool Overlaps(RoiRect first, RoiRect second) =>
        first.X < (long)second.X + second.Width &&
        second.X < (long)first.X + first.Width &&
        first.Y < (long)second.Y + second.Height &&
        second.Y < (long)first.Y + first.Height;

    private static void ValidateRegion(RoiRect? region, ImageSize size)
    {
        if (region is { } actual &&
            (actual.X < 0 || actual.Y < 0 ||
             (long)actual.X + actual.Width > size.Width ||
             (long)actual.Y + actual.Height > size.Height))
        {
            throw OpenCvErrors.Invalid("The target search region must be fully inside the frame.");
        }
    }

    private static void EnsureShapeInsideFrame(TargetShape shape, ImageSize size)
    {
        if ((long)shape.Bounds.X + shape.Bounds.Width > size.Width ||
            (long)shape.Bounds.Y + shape.Bounds.Height > size.Height)
        {
            throw OpenCvErrors.Invalid("The registered target shape must be inside the initialization frame.");
        }
    }

    private static Rect ToRect(RoiRect region) => new(region.X, region.Y, region.Width, region.Height);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
