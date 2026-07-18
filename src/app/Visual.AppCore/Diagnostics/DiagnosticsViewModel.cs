using Visual.VisionBase.Contracts;

namespace Visual.AppCore.Diagnostics;

public sealed class DiagnosticsViewModel
{
    public string EngineId { get; private set; } = "-";
    public string SourceId { get; private set; } = "-";
    public long DroppedCount { get; private set; }
    public double ProcessingMilliseconds { get; private set; }
    public string LastError { get; private set; } = string.Empty;

    public void Update(VisionDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EngineId = record.EngineId ?? EngineId;
        SourceId = record.SourceId ?? SourceId;
        DroppedCount = record.DroppedCount ?? DroppedCount;
        ProcessingMilliseconds = record.ProcessingDuration?.TotalMilliseconds ?? ProcessingMilliseconds;
        if (record.ErrorCode is not null)
        {
            LastError = $"{record.ErrorCode}: {record.Message}";
        }
    }
}
