namespace Visual.Abstractions.Contracts;

public sealed record FrameTiming
{
    public FrameTiming(
        DateTime capturedAt,
        DateTime processingStartedAt,
        DateTime processingCompletedAt)
    {
        if (processingStartedAt < capturedAt || processingCompletedAt < processingStartedAt)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                "Frame timing must satisfy CapturedAt <= ProcessingStartedAt <= ProcessingCompletedAt.");
        }

        CapturedAt = capturedAt;
        ProcessingStartedAt = processingStartedAt;
        ProcessingCompletedAt = processingCompletedAt;
    }

    public DateTime CapturedAt { get; }

    public DateTime ProcessingStartedAt { get; }

    public DateTime ProcessingCompletedAt { get; }

    public TimeSpan ProcessingDuration => ProcessingCompletedAt - ProcessingStartedAt;

    public TimeSpan EndToEndDuration => ProcessingCompletedAt - CapturedAt;
}
