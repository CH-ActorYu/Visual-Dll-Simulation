using System.Runtime.CompilerServices;
using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.IO.Internal;

namespace Visual.IO.Contracts;

public abstract class BufferedFrameSourceBase : IFrameSource
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly int _bufferCapacity;
    private FrameBuffer? _buffer;
    private AsyncSignal? _frameAvailable;
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private VisionException? _terminalError;
    private FrameSourceState _state = FrameSourceState.Closed;
    private long _previousDroppedCount;
    private int _isDisposed;

    protected BufferedFrameSourceBase(string sourceId, int bufferCapacity = FrameBuffer.DefaultCapacity)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame source ID is required.");
        }

        if (bufferCapacity <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame buffer capacity must be positive.");
        }

        SourceId = sourceId;
        _bufferCapacity = bufferCapacity;
    }

    public string SourceId { get; }

    public int Width { get; protected set; }

    public int Height { get; protected set; }

    public double Fps { get; protected set; }

    public bool IsRunning => State == FrameSourceState.Running;

    public FrameSourceState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public long DroppedCount
    {
        get
        {
            lock (_sync)
            {
                return _previousDroppedCount + (_buffer?.DroppedCount ?? 0);
            }
        }
    }

    public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is FrameSourceState.Opened or FrameSourceState.Running)
            {
                return;
            }

            if (State == FrameSourceState.Failed)
            {
                throw new VisionException(VisionErrorCode.NotRunning, "Close the failed frame source before reopening it.");
            }

            try
            {
                await OnOpenAsync(cancellationToken).ConfigureAwait(false);
                SetState(FrameSourceState.Opened);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetState(FrameSourceState.Closed);
                throw;
            }
            catch (Exception exception)
            {
                var error = Normalize(exception, "Failed to open the frame source.");
                SetFailure(error);
                throw error;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == FrameSourceState.Running)
            {
                return;
            }

            if (State != FrameSourceState.Opened)
            {
                throw new VisionException(VisionErrorCode.NotRunning, "The frame source must be opened before it can start.");
            }

            var buffer = new FrameBuffer(_bufferCapacity);
            var signal = new AsyncSignal();
            var captureCancellation = new CancellationTokenSource();

            lock (_sync)
            {
                if (_buffer is not null)
                {
                    _previousDroppedCount += _buffer.DroppedCount;
                }

                _buffer = buffer;
                _frameAvailable?.Dispose();
                _frameAvailable = signal;
                _captureCancellation?.Dispose();
                _captureCancellation = captureCancellation;
                _terminalError = null;
                _state = FrameSourceState.Running;
            }

            try
            {
                await OnStartAsync(cancellationToken).ConfigureAwait(false);
                _captureTask = CaptureGuardedAsync(buffer, signal, captureCancellation.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                buffer.Complete();
                signal.Set();
                SetState(FrameSourceState.Opened);
                throw;
            }
            catch (Exception exception)
            {
                buffer.Complete();
                signal.Set();
                var error = Normalize(exception, "Failed to start the frame source.");
                SetFailure(error);
                throw error;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async IAsyncEnumerable<IImageLease> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        FrameBuffer buffer;
        AsyncSignal signal;
        lock (_sync)
        {
            buffer = _buffer ?? throw new VisionException(VisionErrorCode.NotRunning, "The frame source is not running.");
            signal = _frameAvailable ?? throw new VisionException(VisionErrorCode.NotRunning, "The frame source is not running.");
        }

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (buffer.TryReadLatest() is { } lease)
                {
                    yield return lease;
                    continue;
                }

                if (buffer.IsCompleted)
                {
                    VisionException? terminalError;
                    lock (_sync)
                    {
                        terminalError = _terminalError;
                    }

                    if (terminalError is not null)
                    {
                        throw terminalError;
                    }

                    yield break;
                }

                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == FrameSourceState.Closed)
            {
                return;
            }

            await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                await OnCloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var error = Normalize(exception, "Failed to close the frame source.");
                SetFailure(error);
                throw error;
            }
            SetState(FrameSourceState.Closed);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _captureCancellation?.Dispose();
            _frameAvailable?.Dispose();
            _lifecycleGate.Dispose();
        }
    }

    protected bool TryPublishFrame(IImageLease lease)
    {
        if (lease is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "An image lease is required.");
        }

        FrameBuffer? buffer;
        AsyncSignal? signal;
        lock (_sync)
        {
            buffer = _state == FrameSourceState.Running ? _buffer : null;
            signal = _frameAvailable;
        }

        var accepted = buffer?.TryWrite(lease) == true;
        if (accepted)
        {
            signal?.Set();
        }

        return accepted;
    }

    protected virtual ValueTask OnOpenAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected virtual ValueTask OnStartAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected abstract Task RunCaptureAsync(CancellationToken cancellationToken);

    protected virtual ValueTask OnStopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    protected virtual ValueTask OnCloseAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    private async Task CaptureGuardedAsync(FrameBuffer buffer, AsyncSignal signal, CancellationToken cancellationToken)
    {
        try
        {
            await RunCaptureAsync(cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                if (_state == FrameSourceState.Running)
                {
                    _state = FrameSourceState.Opened;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetFailure(Normalize(exception, "Frame capture failed."));
        }
        finally
        {
            buffer.Complete();
            signal.Set();
        }
    }

    private async ValueTask StopCoreAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? captureCancellation;
        Task? captureTask;
        FrameBuffer? buffer;
        AsyncSignal? signal;

        lock (_sync)
        {
            if (_state is FrameSourceState.Closed or FrameSourceState.Opened)
            {
                return;
            }

            captureCancellation = _captureCancellation;
            captureTask = _captureTask;
            buffer = _buffer;
            signal = _frameAvailable;
        }

        captureCancellation?.Cancel();
        buffer?.Complete();
        signal?.Set();

        if (captureTask is not null)
        {
            await captureTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await OnStopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var error = Normalize(exception, "Failed to stop the frame source.");
            SetFailure(error);
            throw error;
        }

        SetState(FrameSourceState.Opened);
    }

    private void SetFailure(VisionException error)
    {
        lock (_sync)
        {
            _terminalError = error;
            _state = FrameSourceState.Failed;
        }
    }

    private void SetState(FrameSourceState state)
    {
        lock (_sync)
        {
            _state = state;
        }
    }

    private static VisionException Normalize(Exception exception, string message) => exception switch
    {
        VisionException visionException => visionException,
        _ => new VisionException(VisionErrorCode.ModuleSpecific, message, exception)
    };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
    }
}
