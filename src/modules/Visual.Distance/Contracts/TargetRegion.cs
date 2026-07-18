using Visual.Abstractions.Contracts;

namespace Visual.Distance.Contracts;

public sealed record TargetRegion
{
    public TargetRegion(string targetId, RoiRect bounds)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Target ID is required.");
        }

        TargetId = targetId;
        Bounds = bounds;
    }

    public string TargetId { get; }

    public RoiRect Bounds { get; }
}
