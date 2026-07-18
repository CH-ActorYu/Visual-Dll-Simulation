using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Internal;

internal static class DistanceResultBuilder
{
    public static DistanceMeasurement Valid(
        TargetRegion target,
        TargetCandidate candidate,
        double distance,
        DistanceUnit unit,
        FrameInfo frame,
        FrameTiming timing) => new(
            target.TargetId,
            VisionResultStatus.Valid,
            null,
            distance,
            unit,
            candidate.Confidence,
            candidate.BoundingBox,
            candidate.Center,
            candidate.PixelSize,
            timing,
            frame.FrameIndex,
            frame.SourceId);

    public static DistanceMeasurement NotDetected(
        TargetRegion target,
        DistanceUnit unit,
        FrameInfo frame,
        FrameTiming timing) => Status(
            target,
            VisionResultStatus.NotDetected,
            null,
            unit,
            frame,
            timing);

    public static DistanceMeasurement NotCalibrated(
        TargetRegion target,
        DistanceUnit unit,
        FrameInfo frame,
        FrameTiming timing) => Status(
            target,
            VisionResultStatus.NotCalibrated,
            null,
            unit,
            frame,
            timing);

    public static DistanceMeasurement Unavailable(
        TargetRegion target,
        DistanceUnit unit,
        FrameInfo frame,
        FrameTiming timing,
        VisionErrorCode errorCode = VisionErrorCode.EngineUnavailable) => Status(
            target,
            VisionResultStatus.Unavailable,
            errorCode,
            unit,
            frame,
            timing);

    public static DistanceMeasurement Failed(
        TargetRegion target,
        DistanceUnit unit,
        FrameInfo frame,
        FrameTiming timing,
        VisionErrorCode errorCode) => Status(
            target,
            VisionResultStatus.Failed,
            errorCode,
            unit,
            frame,
            timing);

    private static DistanceMeasurement Status(
        TargetRegion target,
        VisionResultStatus status,
        VisionErrorCode? errorCode,
        DistanceUnit unit,
        FrameInfo frame,
        FrameTiming timing) => new(
            target.TargetId,
            status,
            errorCode,
            null,
            unit,
            0,
            null,
            null,
            null,
            timing,
            frame.FrameIndex,
            frame.SourceId);
}
