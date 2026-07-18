using Visual.Abstractions.Contracts;

namespace Visual.Vision.Contracts;

public sealed record TargetCandidate
{
    public TargetCandidate(RoiRect boundingBox, Point2D center, double pixelSize, double confidence)
    {
        if (!double.IsFinite(pixelSize) || pixelSize <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Target pixel size must be positive and finite.");
        }

        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Target confidence must be between 0 and 1.");
        }

        BoundingBox = boundingBox;
        Center = center;
        PixelSize = pixelSize;
        Confidence = confidence;
    }

    public RoiRect BoundingBox { get; }

    public Point2D Center { get; }

    public double PixelSize { get; }

    public double Confidence { get; }
}
