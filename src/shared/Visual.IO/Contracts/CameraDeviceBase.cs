using Visual.Abstractions.Contracts;

namespace Visual.IO.Contracts;

public abstract class CameraDeviceBase : ICameraDevice
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private int _isDisposed;

    protected CameraDeviceBase(CameraDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new VisionException(VisionErrorCode.InvalidInput, "A camera descriptor is required.");
    }

    public CameraDescriptor Descriptor { get; }

    public CameraConnectionState State { get; private set; } = CameraConnectionState.Disconnected;

    public async ValueTask ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == CameraConnectionState.Connected)
            {
                return;
            }

            State = CameraConnectionState.Connecting;
            try
            {
                await OnConnectAsync(cancellationToken).ConfigureAwait(false);
                State = CameraConnectionState.Connected;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                State = CameraConnectionState.Disconnected;
                throw;
            }
            catch (Exception exception)
            {
                State = CameraConnectionState.Failed;
                throw Normalize(exception, "Camera connection failed.");
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<CameraCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        try
        {
            return await OnGetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Normalize(exception, "Failed to query camera capabilities.");
        }
    }

    public async ValueTask SetParameterAsync(
        CameraParameter parameter,
        CameraParameterValue value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (State != CameraConnectionState.Connected)
        {
            throw new VisionException(VisionErrorCode.NotRunning, "The camera must be connected before setting parameters.");
        }

        var range = Descriptor.Capabilities.GetRange(parameter)
            ?? throw new VisionException(VisionErrorCode.InvalidInput, $"Camera parameter {parameter} is not supported.");

        if (value.Automatic && !range.SupportsAuto || !value.Automatic && !range.Contains(value.Value))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, $"Camera parameter {parameter} value is outside its capabilities.");
        }

        try
        {
            await OnSetParameterAsync(parameter, value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Normalize(exception, $"Failed to set camera parameter {parameter}.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
            Interlocked.Exchange(ref _isDisposed, 1);
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }

    protected abstract ValueTask OnConnectAsync(CancellationToken cancellationToken);

    protected abstract ValueTask OnDisconnectAsync(CancellationToken cancellationToken);

    protected virtual ValueTask<CameraCapabilities> OnGetCapabilitiesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Descriptor.Capabilities);

    protected abstract ValueTask OnSetParameterAsync(
        CameraParameter parameter,
        CameraParameterValue value,
        CancellationToken cancellationToken);

    private async ValueTask DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (State == CameraConnectionState.Disconnected)
        {
            return;
        }

        State = CameraConnectionState.Disconnecting;
        try
        {
            await OnDisconnectAsync(cancellationToken).ConfigureAwait(false);
            State = CameraConnectionState.Disconnected;
        }
        catch (Exception exception)
        {
            State = CameraConnectionState.Failed;
            throw Normalize(exception, "Camera disconnection failed.");
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
