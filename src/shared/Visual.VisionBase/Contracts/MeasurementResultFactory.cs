using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Contracts;

public static class MeasurementResultFactory
{
    public static TargetDetectionResult CreateSuccess(
        FrameInfo frameInfo,
        FrameTiming timing,
        TargetCandidate candidate)
    {
        Validate(frameInfo, timing);
        if (candidate is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A successful result requires a target candidate.");
        }

        return new TargetDetectionResult(
            VisionResultStatus.Valid,
            null,
            candidate.Confidence,
            timing,
            frameInfo.FrameIndex,
            frameInfo.SourceId,
            candidate);
    }

    public static TargetDetectionResult CreateNotDetected(FrameInfo frameInfo, FrameTiming timing)
    {
        Validate(frameInfo, timing);
        return new TargetDetectionResult(
            VisionResultStatus.NotDetected,
            null,
            0,
            timing,
            frameInfo.FrameIndex,
            frameInfo.SourceId,
            null);
    }

    public static TargetDetectionResult CreateFailed(
        FrameInfo frameInfo,
        FrameTiming timing,
        VisionErrorCode errorCode)
    {
        Validate(frameInfo, timing);
        return new TargetDetectionResult(
            VisionResultStatus.Failed,
            errorCode,
            0,
            timing,
            frameInfo.FrameIndex,
            frameInfo.SourceId,
            null);
    }

    private static void Validate(FrameInfo frameInfo, FrameTiming timing)
    {
        if (frameInfo is null || timing is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame information and timing are required.");
        }
    }
}
