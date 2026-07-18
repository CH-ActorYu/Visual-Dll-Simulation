using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvGrayscaleOperator : IPreprocessOperator
{
    private readonly OpenCvImageAdapter _adapter = new();

    public IImageLease Apply(ImageFrame source) => OpenCvOperator.Apply(source, _adapter, (input, output) =>
    {
        if (source.Format == PixelFormat.Gray8)
        {
            input.CopyTo(output);
            return;
        }

        var conversion = source.Format switch
        {
            PixelFormat.Bgr24 => ColorConversionCodes.BGR2GRAY,
            PixelFormat.Rgb24 => ColorConversionCodes.RGB2GRAY,
            PixelFormat.Bgra32 => ColorConversionCodes.BGRA2GRAY,
            _ => throw OpenCvErrors.Invalid($"Unsupported grayscale input format {source.Format}.")
        };
        Cv2.CvtColor(input, output, conversion);
    }, PixelFormat.Gray8);
}

public sealed class OpenCvBlurOperator(int kernelSize) : IPreprocessOperator
{
    private readonly OpenCvImageAdapter _adapter = new();

    public int KernelSize { get; } = ValidateKernel(kernelSize);

    public IImageLease Apply(ImageFrame source) => OpenCvOperator.Apply(
        source,
        _adapter,
        (input, output) => Cv2.GaussianBlur(input, output, new Size(KernelSize, KernelSize), 0),
        source.Format);

    private static int ValidateKernel(int value) => value > 0 && value % 2 == 1
        ? value
        : throw OpenCvErrors.Invalid("Gaussian blur kernel size must be a positive odd number.");
}

public sealed class OpenCvThresholdOperator(
    ThresholdMode mode,
    byte manualThreshold,
    ForegroundPolarity polarity) : IPreprocessOperator
{
    private readonly OpenCvImageAdapter _adapter = new();

    public IImageLease Apply(ImageFrame source)
    {
        if (source.Format != PixelFormat.Gray8)
        {
            throw OpenCvErrors.Invalid("Threshold input must be Gray8.");
        }

        return OpenCvOperator.Apply(source, _adapter, (input, output) =>
        {
            var thresholdType = polarity == ForegroundPolarity.Bright
                ? ThresholdTypes.Binary
                : ThresholdTypes.BinaryInv;
            var threshold = manualThreshold;
            if (mode == ThresholdMode.Otsu)
            {
                thresholdType |= ThresholdTypes.Otsu;
                threshold = 0;
            }

            Cv2.Threshold(input, output, threshold, 255, thresholdType);
        }, PixelFormat.Gray8);
    }
}

public sealed class OpenCvMorphologyOperator(int kernelSize) : IPreprocessOperator
{
    private readonly OpenCvImageAdapter _adapter = new();

    public int KernelSize { get; } = kernelSize > 0 && kernelSize % 2 == 1
        ? kernelSize
        : throw OpenCvErrors.Invalid("Morphology kernel size must be a positive odd number.");

    public IImageLease Apply(ImageFrame source)
    {
        if (source.Format != PixelFormat.Gray8)
        {
            throw OpenCvErrors.Invalid("Morphology input must be Gray8.");
        }

        return OpenCvOperator.Apply(source, _adapter, (input, output) =>
        {
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(KernelSize, KernelSize));
            using var opened = new Mat();
            Cv2.MorphologyEx(input, opened, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(opened, output, MorphTypes.Close, kernel);
        }, PixelFormat.Gray8);
    }
}

public sealed class OpenCvEdgeOperator(double lowThreshold = 50, double highThreshold = 150) : IPreprocessOperator
{
    private readonly OpenCvImageAdapter _adapter = new();

    public IImageLease Apply(ImageFrame source)
    {
        if (source.Format != PixelFormat.Gray8 || lowThreshold < 0 || highThreshold <= lowThreshold)
        {
            throw OpenCvErrors.Invalid("Canny input or thresholds are invalid.");
        }

        return OpenCvOperator.Apply(
            source,
            _adapter,
            (input, output) => Cv2.Canny(input, output, lowThreshold, highThreshold),
            PixelFormat.Gray8);
    }
}

internal static class OpenCvOperator
{
    public static IImageLease Apply(
        ImageFrame source,
        OpenCvImageAdapter adapter,
        Action<Mat, Mat> operation,
        PixelFormat outputFormat)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using var input = adapter.ToMat(source);
            using var output = new Mat();
            operation(input.Mat, output);
            return adapter.FromMat(output, source.Info, outputFormat);
        }
        catch (Exception exception)
        {
            throw OpenCvErrors.Normalize(exception, "OpenCV preprocessing failed.");
        }
    }
}
