using System.Collections.ObjectModel;
using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Distance.Contracts;

public sealed record DistanceRequest
{
    public DistanceRequest(ImageFrame frame, IEnumerable<TargetRegion> targets)
    {
        Frame = frame ?? throw Invalid("A frame is required.");
        var snapshot = (targets ?? throw Invalid("Targets are required.")).ToArray();
        ValidateTargets(snapshot);
        Targets = new ReadOnlyCollection<TargetRegion>(snapshot);
    }

    public ImageFrame Frame { get; }

    public IReadOnlyList<TargetRegion> Targets { get; }

    internal static void ValidateTargets(IReadOnlyList<TargetRegion> targets)
    {
        if (targets.Count == 0 || targets.Any(target => target is null))
        {
            throw Invalid("At least one non-null target is required.");
        }

        var duplicate = targets
            .GroupBy(target => target.TargetId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw Invalid($"Target ID '{duplicate.Key}' is duplicated.");
        }
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
