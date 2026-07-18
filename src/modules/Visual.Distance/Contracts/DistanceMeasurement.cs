using Visual.Abstractions.Contracts;

namespace Visual.Distance.Contracts;

public sealed record DistanceMeasurement : VisionResultBase
{
    internal DistanceMeasurement(
        string targetId,
        VisionResultStatus status,
        VisionErrorCode? errorCode,
        double? distance,
        DistanceUnit unit,
        double confidence,
        Point2D? targetCenter,
        double? targetPixelSize,
        FrameTiming timing,
        long frameIndex,
        string? sourceId)
        : base(status, errorCode, confidence, timing, frameIndex, sourceId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Measurement target ID is required.");
        }

        if (status == VisionResultStatus.Valid &&
            (distance is null || !double.IsFinite(distance.Value) || distance <= 0 ||
             targetCenter is null || targetPixelSize is null || targetPixelSize <= 0))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A valid measurement requires distance, center and pixel size.");
        }

        if (status != VisionResultStatus.Valid && distance is not null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A non-valid measurement cannot contain a distance.");
        }

        TargetId = targetId;
        Distance = distance;
        Unit = unit;
        TargetCenter = targetCenter;
        TargetPixelSize = targetPixelSize;
    }

    public string TargetId { get; }

    public double? Distance { get; }

    public DistanceUnit Unit { get; }

    public Point2D? TargetCenter { get; }

    public double? TargetPixelSize { get; }
}
