using Visual.Abstractions.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed record VisionDiagnosticRecord(
    VisionDiagnosticKind Kind,
    DateTime Timestamp,
    string? SourceId,
    long? FrameIndex,
    string? EngineId,
    TimeSpan? ProcessingDuration,
    long? DroppedCount,
    VisionErrorCode? ErrorCode,
    string? Message);
