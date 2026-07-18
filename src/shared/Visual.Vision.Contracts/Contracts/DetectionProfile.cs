using Visual.Abstractions.Contracts;

namespace Visual.Vision.Contracts;

public sealed record DetectionProfile
{
    public DetectionProfile(
        string profileId,
        ThresholdMode thresholdMode,
        byte manualThreshold,
        ForegroundPolarity foregroundPolarity,
        int blurKernelSize,
        int morphologyKernelSize,
        double minAreaRatio,
        double maxAreaRatio,
        double minAspectRatio,
        double maxAspectRatio,
        int borderMargin,
        double minimumConfidence,
        PixelMeasureAxis measureAxis,
        int redetectInterval)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw Invalid("Detection profile ID is required.");
        }

        ValidateOddKernel(blurKernelSize, nameof(blurKernelSize));
        ValidateOddKernel(morphologyKernelSize, nameof(morphologyKernelSize));
        ValidateRange(minAreaRatio, maxAreaRatio, 0, 1, "area ratio");
        ValidateRange(minAspectRatio, maxAspectRatio, 0, double.MaxValue, "aspect ratio");

        if (borderMargin < 0)
        {
            throw Invalid("Border margin cannot be negative.");
        }

        if (!double.IsFinite(minimumConfidence) || minimumConfidence is < 0 or > 1)
        {
            throw Invalid("Minimum confidence must be between 0 and 1.");
        }

        if (redetectInterval <= 0)
        {
            throw Invalid("Redetection interval must be positive.");
        }

        ProfileId = profileId;
        ThresholdMode = thresholdMode;
        ManualThreshold = manualThreshold;
        ForegroundPolarity = foregroundPolarity;
        BlurKernelSize = blurKernelSize;
        MorphologyKernelSize = morphologyKernelSize;
        MinAreaRatio = minAreaRatio;
        MaxAreaRatio = maxAreaRatio;
        MinAspectRatio = minAspectRatio;
        MaxAspectRatio = maxAspectRatio;
        BorderMargin = borderMargin;
        MinimumConfidence = minimumConfidence;
        MeasureAxis = measureAxis;
        RedetectInterval = redetectInterval;
    }

    public string ProfileId { get; }

    public ThresholdMode ThresholdMode { get; }

    public byte ManualThreshold { get; }

    public ForegroundPolarity ForegroundPolarity { get; }

    public int BlurKernelSize { get; }

    public int MorphologyKernelSize { get; }

    public double MinAreaRatio { get; }

    public double MaxAreaRatio { get; }

    public double MinAspectRatio { get; }

    public double MaxAspectRatio { get; }

    public int BorderMargin { get; }

    public double MinimumConfidence { get; }

    public PixelMeasureAxis MeasureAxis { get; }

    public int RedetectInterval { get; }

    public static DetectionProfile CreateDefault() => new(
        "default-otsu-bright-v1",
        ThresholdMode.Otsu,
        128,
        ForegroundPolarity.Bright,
        5,
        3,
        0.01,
        0.90,
        0.1,
        10,
        2,
        0.5,
        PixelMeasureAxis.Horizontal,
        10);

    private static void ValidateOddKernel(int value, string name)
    {
        if (value <= 0 || value % 2 == 0)
        {
            throw Invalid($"{name} must be a positive odd number.");
        }
    }

    private static void ValidateRange(double minimum, double maximum, double exclusiveLowerBound, double inclusiveUpperBound, string name)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) ||
            minimum <= exclusiveLowerBound || maximum > inclusiveUpperBound || minimum > maximum)
        {
            throw Invalid($"The {name} range is invalid.");
        }
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
