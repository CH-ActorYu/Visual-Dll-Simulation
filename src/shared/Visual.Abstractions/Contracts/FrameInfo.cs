namespace Visual.Abstractions.Contracts;

public sealed record FrameInfo
{
    public FrameInfo(long frameIndex, DateTime timestamp, string? sourceId, ImageSize size)
    {
        if (frameIndex < 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame index cannot be negative.");
        }

        if (sourceId is not null && string.IsNullOrWhiteSpace(sourceId))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Source ID cannot be empty.");
        }

        if (!size.IsValid)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame image size is invalid.");
        }

        FrameIndex = frameIndex;
        Timestamp = timestamp;
        SourceId = sourceId;
        Size = size;
    }

    public long FrameIndex { get; }

    public DateTime Timestamp { get; }

    public string? SourceId { get; }

    public ImageSize Size { get; }
}
