using Visual.Abstractions.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed class VisionDiagnostics
{
    public const int DefaultCapacity = 256;

    private readonly object _sync = new();
    private readonly Queue<VisionDiagnosticRecord> _records;
    private readonly IVisionDiagnosticSink[] _sinks;
    private long _sinkFailureCount;

    public VisionDiagnostics(
        IEnumerable<IVisionDiagnosticSink>? sinks = null,
        int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Diagnostic capacity must be positive.");
        }

        Capacity = capacity;
        _records = new Queue<VisionDiagnosticRecord>(capacity);
        _sinks = sinks?.ToArray() ?? [];
        if (_sinks.Any(sink => sink is null))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Diagnostic sinks cannot contain null entries.");
        }
    }

    public int Capacity { get; }

    public long SinkFailureCount => Interlocked.Read(ref _sinkFailureCount);

    public IReadOnlyList<VisionDiagnosticRecord> Snapshot()
    {
        lock (_sync)
        {
            return [.. _records];
        }
    }

    public void RuntimeStarted(string sourceId, string? engineId = null) => Publish(new(
        VisionDiagnosticKind.RuntimeStarted,
        DateTime.UtcNow,
        sourceId,
        null,
        engineId,
        null,
        null,
        null,
        "Vision runtime started."));

    public void RuntimeStopped(string sourceId, string? engineId = null) => Publish(new(
        VisionDiagnosticKind.RuntimeStopped,
        DateTime.UtcNow,
        sourceId,
        null,
        engineId,
        null,
        null,
        null,
        "Vision runtime stopped."));

    public void FrameProcessed(
        string? sourceId,
        long frameIndex,
        string? engineId,
        TimeSpan processingDuration,
        long droppedCount) => Publish(new(
            VisionDiagnosticKind.FrameProcessed,
            DateTime.UtcNow,
            sourceId,
            frameIndex,
            engineId,
            processingDuration,
            droppedCount,
            null,
            null));

    public void FrameDropped(string? sourceId, long droppedCount, string? engineId = null) => Publish(new(
        VisionDiagnosticKind.FrameDropped,
        DateTime.UtcNow,
        sourceId,
        null,
        engineId,
        null,
        droppedCount,
        null,
        "Frames were dropped before processing."));

    public void EngineFailed(
        string? sourceId,
        string? engineId,
        VisionErrorCode errorCode,
        string message,
        long? frameIndex = null) => Publish(new(
            VisionDiagnosticKind.EngineFailed,
            DateTime.UtcNow,
            sourceId,
            frameIndex,
            engineId,
            null,
            null,
            errorCode,
            message));

    public void ConfigurationRejected(string? engineId, VisionErrorCode errorCode, string message) => Publish(new(
        VisionDiagnosticKind.ConfigurationRejected,
        DateTime.UtcNow,
        null,
        null,
        engineId,
        null,
        null,
        errorCode,
        message));

    private void Publish(VisionDiagnosticRecord diagnostic)
    {
        lock (_sync)
        {
            if (_records.Count == Capacity)
            {
                _records.Dequeue();
            }

            _records.Enqueue(diagnostic);
        }

        foreach (var sink in _sinks)
        {
            try
            {
                sink.Write(diagnostic);
            }
            catch
            {
                Interlocked.Increment(ref _sinkFailureCount);
            }
        }
    }
}
