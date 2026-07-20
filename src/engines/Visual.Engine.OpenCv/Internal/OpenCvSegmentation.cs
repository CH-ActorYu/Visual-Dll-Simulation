using OpenCvSharp;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Internal;

internal static class OpenCvSegmentation
{
    public static Mat ToGray(Mat source, PixelFormat format)
    {
        var gray = new Mat();
        try
        {
            if (format == PixelFormat.Gray8)
            {
                source.CopyTo(gray);
                return gray;
            }

            var conversion = format switch
            {
                PixelFormat.Bgr24 => ColorConversionCodes.BGR2GRAY,
                PixelFormat.Rgb24 => ColorConversionCodes.RGB2GRAY,
                PixelFormat.Bgra32 => ColorConversionCodes.BGRA2GRAY,
                _ => throw OpenCvErrors.Invalid($"Unsupported segmentation input format {format}.")
            };
            Cv2.CvtColor(source, gray, conversion);
            return gray;
        }
        catch
        {
            gray.Dispose();
            throw;
        }
    }

    public static Mat Segment(Mat source, PixelFormat format, DetectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        using var gray = ToGray(source, format);
        using var blurred = new Mat();
        Cv2.GaussianBlur(
            gray,
            blurred,
            new Size(profile.BlurKernelSize, profile.BlurKernelSize),
            0);
        using var thresholded = new Mat();
        var thresholdType = profile.ForegroundPolarity == ForegroundPolarity.Bright
            ? ThresholdTypes.Binary
            : ThresholdTypes.BinaryInv;
        var threshold = profile.ManualThreshold;
        if (profile.ThresholdMode == ThresholdMode.Otsu)
        {
            thresholdType |= ThresholdTypes.Otsu;
            threshold = 0;
        }

        Cv2.Threshold(blurred, thresholded, threshold, 255, thresholdType);
        using var kernel = Cv2.GetStructuringElement(
            MorphShapes.Rect,
            new Size(profile.MorphologyKernelSize, profile.MorphologyKernelSize));
        using var opened = new Mat();
        Cv2.MorphologyEx(thresholded, opened, MorphTypes.Open, kernel);
        var result = new Mat();
        Cv2.MorphologyEx(opened, result, MorphTypes.Close, kernel);
        return result;
    }
}
