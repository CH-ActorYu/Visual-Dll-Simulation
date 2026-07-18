using System.Diagnostics;
using Visual.Abstractions.Contracts;
using Visual.IO.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed class VisionRuntime : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly IFrameSource _frameSource;
    private readonly IFrameProcessor _processor;
    private readonly VisionDiagnostics _diagnostics;
    private readonly string? _engineId;
    private CancellationTokenSource? _runtimeCancellation;
    private Task? _processingTask;
    private VisionRuntimeState _state = VisionRuntimeState.Created;
    private VisionException? _lastError;

    public VisionRuntime(
        IFrameSource frameSource,
        IFrameProcessor processor,
        VisionDiagnostics? diagnostics = null,
        string? engineId = null)
    {
        _frameSource = frameSource ?? throw Invalid("A frame source is required.");
        _processor = processor ?? throw Invalid("A frame processor is required.");
        _diagnostics = diagnostics ?? new VisionDiagnostics();
        _engineId = engineId;
    }

    public VisionRuntimeState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public VisionException? LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
    }

    public Task Completion
    {
        get
        {
            lock (_sync)
            {
                return _processingTask ?? Task.CompletedTask;
            }
        }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == VisionRuntimeState.Running)
            {
                return;
            }

            if (State == VisionRuntimeState.Disposed)
            {
                throw new ObjectDisposedException(nameof(VisionRuntime));
            }

            SetState(VisionRuntimeState.Starting);
            lock (_sync)
            {
                _lastError = null;
            }

            try
            {
                await _frameSource.OpenAsync(cancellationToken).ConfigureAwait(false);
                await _frameSource.StartAsync(cancellationToken).ConfigureAwait(false);
                var runtimeCancellation = new CancellationTokenSource();
                lock (_sync)
                {
                    _runtimeCancellation?.Dispose();
                    _runtimeCancellation = runtimeCancellation;
                    _state = VisionRuntimeState.Running;
                    _processingTask = ProcessFramesAsync(runtimeCancellation.Token);
                }

                _diagnostics.RuntimeStarted(_frameSource.SourceId, _engineId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await _frameSource.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                SetState(VisionRuntimeState.Stopped);
                throw;
            }
            catch (Exception exception)
            {
                var error = Normalize(exception, "Failed to start the vision runtime.");
                SetFailure(error);
                await _frameSource.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                throw error;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is VisionRuntimeState.Created or VisionRuntimeState.Stopped or VisionRuntimeState.Disposed)
            {
                return;
            }

            SetState(VisionRuntimeState.Stopping);
            _runtimeCancellation?.Cancel();
            VisionException? failure = null;

            try
            {
                await _frameSource.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = Normalize(exception, "Failed to stop the frame source.");
            }

            var processingTask = Completion;
            try
            {
                await processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_runtimeCancellation?.IsCancellationRequested == true)
            {
            }
            catch (Exception exception)
            {
                failure ??= Normalize(exception, "Vision processing failed.");
            }

            try
            {
                await _frameSource.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure ??= Normalize(exception, "Failed to close the frame source.");
            }

            SetState(VisionRuntimeState.Stopped);
            _diagnostics.RuntimeStopped(_frameSource.SourceId, _engineId);

            if (failure is not null)
            {
                lock (_sync)
                {
                    _lastError ??= failure;
                }

                throw failure;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public void CancelCurrentWork() => _runtimeCancellation?.Cancel();

    public async ValueTask DisposeAsync()
    {
        if (State == VisionRuntimeState.Disposed)
        {
            return;
        }

        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await _frameSource.DisposeAsync().ConfigureAwait(false);
            _runtimeCancellation?.Dispose();
            SetState(VisionRuntimeState.Disposed);
            _lifecycleGate.Dispose();
        }
    }

    private async Task ProcessFramesAsync(CancellationToken cancellationToken)
    {
        var previousDroppedCount = _frameSource.DroppedCount;
        try
        {
            await foreach (var lease in _frameSource.ReadFramesAsync(cancellationToken).ConfigureAwait(false))
            {
                using (lease)
                {
                    var startTimestamp = Stopwatch.GetTimestamp();
                    await _processor.ProcessAsync(lease.Frame, cancellationToken).ConfigureAwait(false);
                    var processingDuration = Stopwatch.GetElapsedTime(startTimestamp);
                    var droppedCount = _frameSource.DroppedCount;
                    _diagnostics.FrameProcessed(
                        lease.Frame.Info.SourceId,
                        lease.Frame.Info.FrameIndex,
                        _engineId,
                        processingDuration,
                        droppedCount);

                    if (droppedCount > previousDroppedCount)
                    {
                        _diagnostics.FrameDropped(lease.Frame.Info.SourceId, droppedCount, _engineId);
                        previousDroppedCount = droppedCount;
                    }
                }
            }

            SetState(VisionRuntimeState.Completed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = Normalize(exception, "Vision processing failed.");
            SetFailure(error);
            _diagnostics.EngineFailed(
                _frameSource.SourceId,
                _engineId,
                error.ErrorCode,
                error.Message);
            throw error;
        }
    }

    private void SetFailure(VisionException error)
    {
        lock (_sync)
        {
            _lastError = error;
            _state = VisionRuntimeState.Failed;
        }
    }

    private void SetState(VisionRuntimeState state)
    {
        lock (_sync)
        {
            _state = state;
        }
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);

    private static VisionException Normalize(Exception exception, string message) => exception switch
    {
        VisionException visionException => visionException,
        _ => new VisionException(VisionErrorCode.ModuleSpecific, message, exception)
    };
}
