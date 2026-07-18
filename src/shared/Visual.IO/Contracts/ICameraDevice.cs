namespace Visual.IO.Contracts;

public interface ICameraDevice : IAsyncDisposable
{
    CameraDescriptor Descriptor { get; }

    CameraConnectionState State { get; }

    ValueTask ConnectAsync(CancellationToken cancellationToken = default);

    ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

    ValueTask<CameraCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    ValueTask SetParameterAsync(
        CameraParameter parameter,
        CameraParameterValue value,
        CancellationToken cancellationToken = default);
}
