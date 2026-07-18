using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed record TargetDetectionResult : VisionResultBase
{
    internal TargetDetectionResult(
        VisionResultStatus status,
        VisionErrorCode? errorCode,
        double confidence,
        FrameTiming timing,
        long frameIndex,
        string? sourceId,
        TargetCandidate? candidate)
        : base(status, errorCode, confidence, timing, frameIndex, sourceId)
    {
        Candidate = candidate;
    }

    public TargetCandidate? Candidate { get; }
}
