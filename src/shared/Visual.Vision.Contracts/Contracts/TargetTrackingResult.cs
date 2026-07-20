using Visual.Abstractions.Contracts;

namespace Visual.Vision.Contracts;

public sealed record TargetTrackingResult
{
    public TargetTrackingResult(TargetTrackingStatus status, TargetShape? shape, double confidence)
    {
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw Invalid("Target tracking confidence must be between 0 and 1.");
        }

        if (status == TargetTrackingStatus.Tracking && shape is null)
        {
            throw Invalid("A tracking result requires a current target shape.");
        }

        if (status != TargetTrackingStatus.Tracking && shape is not null)
        {
            throw Invalid("Only a tracking result can contain a current target shape.");
        }

        Status = status;
        Shape = shape;
        Confidence = confidence;
    }

    public TargetTrackingStatus Status { get; }

    public TargetShape? Shape { get; }

    public double Confidence { get; }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
