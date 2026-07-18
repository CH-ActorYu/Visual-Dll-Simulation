namespace Visual.IO.Contracts;

public interface ICameraDeviceProvider
{
    ValueTask<IReadOnlyList<CameraDescriptor>> EnumerateAsync(CancellationToken cancellationToken = default);

    ValueTask<ICameraDevice> OpenAsync(
        CameraDescriptor descriptor,
        CancellationToken cancellationToken = default);
}
