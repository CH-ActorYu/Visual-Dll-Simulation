using Visual.Abstractions.Contracts;
using System.Collections.ObjectModel;

namespace Visual.IO.Contracts;

public sealed record FrameSourceDescriptor
{
    public FrameSourceDescriptor(
        string sourceId,
        FrameSourceKind kind,
        string? location = null,
        IReadOnlyDictionary<string, string>? options = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame source ID is required.");
        }

        if (kind is FrameSourceKind.VideoFile or FrameSourceKind.ImageFolder && string.IsNullOrWhiteSpace(location))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A location is required for file-based frame sources.");
        }

        SourceId = sourceId;
        Kind = kind;
        Location = location;
        Options = new ReadOnlyDictionary<string, string>(
            options is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(options, StringComparer.OrdinalIgnoreCase));
    }

    public string SourceId { get; }

    public FrameSourceKind Kind { get; }

    public string? Location { get; }

    public IReadOnlyDictionary<string, string> Options { get; }
}
