using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvBlobDetector : ITargetDetector
{
    private readonly DetectionProfile _profile;
    private readonly OpenCvImageAdapter _adapter = new();

    public OpenCvBlobDetector(DetectionProfile profile)
    {
        _profile = profile ?? throw OpenCvErrors.Invalid("A detection profile is required.");
    }

    public TargetCandidate? Detect(ImageFrame frame, RoiRect roi)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var actualRoi = roi.ClampTo(frame.Info.Size);
        try
        {
            using var full = _adapter.ToMat(frame);
            var region = new Rect(actualRoi.X, actualRoi.Y, actualRoi.Width, actualRoi.Height);
            using var roiMat = new Mat(full.Mat, region);
            using var processed = OpenCvSegmentation.Segment(roiMat, frame.Format, _profile);
            Cv2.FindContours(
                processed,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            TargetCandidate? best = null;
            double bestArea = -1;
            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                var areaRatio = area / actualRoi.Area;
                var localBox = Cv2.BoundingRect(contour);
                if (!IsValid(localBox, areaRatio))
                {
                    continue;
                }

                var confidence = Score(areaRatio, localBox, actualRoi);
                if (confidence < _profile.MinimumConfidence || area < bestArea)
                {
                    continue;
                }

                var box = new RoiRect(
                    actualRoi.X + localBox.X,
                    actualRoi.Y + localBox.Y,
                    localBox.Width,
                    localBox.Height);
                var center = new Point2D(box.X + box.Width / 2d, box.Y + box.Height / 2d);
                best = new TargetCandidate(box, center, Measure(localBox), confidence);
                bestArea = area;
            }

            return best;
        }
        catch (Exception exception)
        {
            throw OpenCvErrors.Normalize(exception, "OpenCV Blob detection failed.");
        }
    }

    private bool IsValid(Rect box, double areaRatio)
    {
        if (areaRatio < _profile.MinAreaRatio || areaRatio > _profile.MaxAreaRatio || box.Height <= 0)
        {
            return false;
        }

        var aspectRatio = (double)box.Width / box.Height;
        if (aspectRatio < _profile.MinAspectRatio || aspectRatio > _profile.MaxAspectRatio)
        {
            return false;
        }

        return box.X >= _profile.BorderMargin &&
               box.Y >= _profile.BorderMargin;
    }

    private double Score(double areaRatio, Rect box, RoiRect roi)
    {
        var touchesRight = box.Right > roi.Width - _profile.BorderMargin;
        var touchesBottom = box.Bottom > roi.Height - _profile.BorderMargin;
        if (touchesRight || touchesBottom)
        {
            return 0;
        }

        var normalizedArea = Math.Clamp(
            (areaRatio - _profile.MinAreaRatio) /
            Math.Max(_profile.MaxAreaRatio - _profile.MinAreaRatio, double.Epsilon),
            0,
            1);
        return Math.Clamp(0.75 + normalizedArea * 0.25, 0, 1);
    }

    private double Measure(Rect box) => _profile.MeasureAxis switch
    {
        PixelMeasureAxis.Horizontal => box.Width,
        PixelMeasureAxis.Vertical => box.Height,
        PixelMeasureAxis.MajorAxis => Math.Max(box.Width, box.Height),
        PixelMeasureAxis.MinorAxis => Math.Min(box.Width, box.Height),
        _ => throw OpenCvErrors.Invalid($"Unsupported measure axis {_profile.MeasureAxis}.")
    };
}
