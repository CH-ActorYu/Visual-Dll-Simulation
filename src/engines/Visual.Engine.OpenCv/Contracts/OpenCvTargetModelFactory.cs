using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvTargetModelFactory : ITargetModelFactory
{
    private readonly OpenCvImageAdapter _adapter;

    public OpenCvTargetModelFactory()
        : this(new ImageFrameFactory())
    {
    }

    internal OpenCvTargetModelFactory(ImageFrameFactory frameFactory)
    {
        _adapter = new OpenCvImageAdapter(frameFactory ?? throw new ArgumentNullException(nameof(frameFactory)));
    }

    public ITargetModel Create(
        ImageFrame referenceFrame,
        TargetSelection selection,
        DetectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(referenceFrame);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(profile);
        EnsureSelectionInsideFrame(selection.Bounds, referenceFrame.Info.Size);

        try
        {
            using var full = _adapter.ToMat(referenceFrame);
            var selectionRect = ToRect(selection.Bounds);
            using var selectionView = new Mat(full.Mat, selectionRect);
            using var selectionMask = CreateSelectionMask(selection, selection.Bounds);
            using var segmented = OpenCvSegmentation.Segment(selectionView, referenceFrame.Format, profile);
            using var constrained = new Mat();
            Cv2.BitwiseAnd(segmented, selectionMask, constrained);

            var contour = SelectContour(constrained, selection.Bounds, profile);
            using var targetMask = new Mat(selectionRect.Height, selectionRect.Width, MatType.CV_8UC1, Scalar.Black);
            Cv2.DrawContours(targetMask, [contour], 0, Scalar.White, -1);

            var localBounds = Cv2.BoundingRect(contour);
            var shape = OpenCvTargetGeometry.CreateShape(
                contour,
                selection.Bounds.X,
                selection.Bounds.Y,
                profile.MeasureAxis);
            using var maskView = new Mat(targetMask, localBounds);
            Mat? ownedMask = maskView.Clone();
            Mat? ownedTemplate = null;
            IImageLease? preview = null;
            try
            {
                using var gray = OpenCvSegmentation.ToGray(selectionView, referenceFrame.Format);
                using var grayView = new Mat(gray, localBounds);
                ownedTemplate = new Mat();
                Cv2.BitwiseAnd(grayView, maskView, ownedTemplate);
                using var previewMat = CreateTransparentPreview(selectionView, localBounds, maskView, referenceFrame.Format);
                preview = _adapter.FromMat(
                    previewMat,
                    new FrameInfo(
                        referenceFrame.Info.FrameIndex,
                        referenceFrame.Info.Timestamp,
                        referenceFrame.Info.SourceId,
                        new ImageSize(localBounds.Width, localBounds.Height)),
                    PixelFormat.Bgra32);

                var model = new OpenCvTargetModel(
                    shape,
                    profile.ProfileId,
                    ownedTemplate,
                    ownedMask,
                    preview);
                ownedTemplate = null;
                ownedMask = null;
                preview = null;
                return model;
            }
            finally
            {
                ownedTemplate?.Dispose();
                ownedMask?.Dispose();
                preview?.Dispose();
            }
        }
        catch (Exception exception)
        {
            throw OpenCvErrors.Normalize(exception, "OpenCV target registration failed.");
        }
    }

    private static Mat CreateSelectionMask(TargetSelection selection, RoiRect bounds)
    {
        var mask = new Mat(bounds.Height, bounds.Width, MatType.CV_8UC1, Scalar.Black);
        try
        {
            var polygon = selection.Polygon
                .Select(point => new Point(
                    checked((int)Math.Round(point.X - bounds.X, MidpointRounding.AwayFromZero)),
                    checked((int)Math.Round(point.Y - bounds.Y, MidpointRounding.AwayFromZero))))
                .ToArray();
            Cv2.FillPoly(mask, [polygon], Scalar.White);
            return mask;
        }
        catch
        {
            mask.Dispose();
            throw;
        }
    }

    private static Point[] SelectContour(Mat constrained, RoiRect selectionBounds, DetectionProfile profile)
    {
        Cv2.FindContours(
            constrained,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        Point[]? best = null;
        var bestArea = 0d;
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            var areaRatio = area / selectionBounds.Area;
            var box = Cv2.BoundingRect(contour);
            if (box.Height <= 0 || areaRatio < profile.MinAreaRatio || areaRatio > 1)
            {
                continue;
            }

            var aspectRatio = (double)box.Width / box.Height;
            if (aspectRatio < profile.MinAspectRatio || aspectRatio > profile.MaxAspectRatio || area <= bestArea)
            {
                continue;
            }

            best = contour;
            bestArea = area;
        }

        return best ?? throw OpenCvErrors.Module("No target contour matched the registration profile.");
    }

    private static Mat CreateTransparentPreview(
        Mat selectionView,
        Rect localBounds,
        Mat mask,
        PixelFormat format)
    {
        using var source = new Mat(selectionView, localBounds);
        var preview = new Mat();
        try
        {
            ColorConversionCodes? conversion = format switch
            {
                PixelFormat.Gray8 => ColorConversionCodes.GRAY2BGRA,
                PixelFormat.Bgr24 => ColorConversionCodes.BGR2BGRA,
                PixelFormat.Rgb24 => ColorConversionCodes.RGB2BGRA,
                PixelFormat.Bgra32 => null,
                _ => throw OpenCvErrors.Invalid($"Unsupported preview input format {format}.")
            };
            if (conversion is { } conversionCode)
            {
                Cv2.CvtColor(source, preview, conversionCode);
            }
            else
            {
                source.CopyTo(preview);
            }

            var channels = Cv2.Split(preview);
            try
            {
                mask.CopyTo(channels[3]);
                Cv2.Merge(channels, preview);
            }
            finally
            {
                foreach (var channel in channels)
                {
                    channel.Dispose();
                }
            }

            return preview;
        }
        catch
        {
            preview.Dispose();
            throw;
        }
    }

    private static void EnsureSelectionInsideFrame(RoiRect bounds, ImageSize imageSize)
    {
        if (bounds.X < 0 || bounds.Y < 0 ||
            (long)bounds.X + bounds.Width > imageSize.Width ||
            (long)bounds.Y + bounds.Height > imageSize.Height)
        {
            throw OpenCvErrors.Invalid("Target selection must be fully inside the reference frame.");
        }
    }

    private static Rect ToRect(RoiRect bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
}
