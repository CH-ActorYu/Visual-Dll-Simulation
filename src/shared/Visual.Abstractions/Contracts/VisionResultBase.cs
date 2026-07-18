namespace Visual.Abstractions.Contracts;

public abstract record VisionResultBase
{
    protected VisionResultBase(
        VisionResultStatus status,
        VisionErrorCode? errorCode,
        double confidence,
        FrameTiming timing,
        long frameIndex,
        string? sourceId)
    {
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Confidence must be between 0 and 1.");
        }

        if (frameIndex < 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame index cannot be negative.");
        }

        if (status == VisionResultStatus.Valid && errorCode is not null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A valid result cannot contain an error code.");
        }

        Status = status;
        ErrorCode = errorCode;
        Confidence = confidence;
        Timing = timing ?? throw new VisionException(VisionErrorCode.InvalidInput, "Result timing is required.");
        FrameIndex = frameIndex;
        SourceId = sourceId;
    }

    public VisionResultStatus Status { get; }

    public VisionErrorCode? ErrorCode { get; }

    public double Confidence { get; }

    public FrameTiming Timing { get; }

    public long FrameIndex { get; }

    public string? SourceId { get; }
}
