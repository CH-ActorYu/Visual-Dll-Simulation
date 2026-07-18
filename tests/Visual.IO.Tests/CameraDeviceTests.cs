using Visual.Abstractions.Contracts;
using Visual.IO.Contracts;

namespace Visual.IO.Tests;

public sealed class CameraDeviceTests
{
    [Fact]
    public async Task Provider_should_separate_enumeration_from_device_connection()
    {
        var descriptor = CreateDescriptor();
        var provider = new TestCameraProvider(descriptor);

        var descriptors = await provider.EnumerateAsync();
        await using var device = await provider.OpenAsync(descriptor);

        Assert.Single(descriptors);
        Assert.Equal(descriptor, descriptors[0]);
        Assert.Equal(CameraConnectionState.Disconnected, device.State);
        Assert.Equal(1, provider.EnumerateCount);
        Assert.Equal(1, provider.OpenCount);

        await device.ConnectAsync();
        Assert.Equal(CameraConnectionState.Connected, device.State);
    }

    [Fact]
    public async Task Connect_and_disconnect_should_be_idempotent()
    {
        await using var device = new TestCameraDevice(CreateDescriptor());

        await device.ConnectAsync();
        await device.ConnectAsync();
        await device.DisconnectAsync();
        await device.DisconnectAsync();

        Assert.Equal(1, device.ConnectCount);
        Assert.Equal(1, device.DisconnectCount);
        Assert.Equal(CameraConnectionState.Disconnected, device.State);
    }

    [Fact]
    public async Task Capabilities_should_be_available_independently_of_connection()
    {
        await using var device = new TestCameraDevice(CreateDescriptor());

        var capabilities = await device.GetCapabilitiesAsync();

        Assert.Contains(new ImageSize(640, 480), capabilities.Resolutions);
        Assert.Contains(30, capabilities.FrameRates);
    }

    [Fact]
    public async Task Set_parameter_should_validate_connection_support_and_range()
    {
        await using var device = new TestCameraDevice(CreateDescriptor());
        var notConnected = await Assert.ThrowsAsync<VisionException>(() =>
            device.SetParameterAsync(CameraParameter.Exposure, new CameraParameterValue(5)).AsTask());
        await device.ConnectAsync();
        var unsupported = await Assert.ThrowsAsync<VisionException>(() =>
            device.SetParameterAsync(CameraParameter.Focus, new CameraParameterValue(5)).AsTask());
        var outsideRange = await Assert.ThrowsAsync<VisionException>(() =>
            device.SetParameterAsync(CameraParameter.Exposure, new CameraParameterValue(50)).AsTask());
        await device.SetParameterAsync(CameraParameter.Exposure, new CameraParameterValue(5));

        Assert.Equal(VisionErrorCode.NotRunning, notConnected.ErrorCode);
        Assert.Equal(VisionErrorCode.InvalidInput, unsupported.ErrorCode);
        Assert.Equal(VisionErrorCode.InvalidInput, outsideRange.ErrorCode);
        Assert.Equal(1, device.SetParameterCount);
    }

    [Fact]
    public async Task Native_connection_error_should_be_normalized()
    {
        await using var device = new TestCameraDevice(CreateDescriptor())
        {
            ConnectError = new InvalidOperationException("native failure")
        };

        var exception = await Assert.ThrowsAsync<VisionException>(() => device.ConnectAsync().AsTask());

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(CameraConnectionState.Failed, device.State);
    }

    [Fact]
    public async Task Native_parameter_error_should_be_normalized()
    {
        await using var device = new TestCameraDevice(CreateDescriptor())
        {
            ParameterError = new InvalidOperationException("native parameter failure")
        };
        await device.ConnectAsync();

        var exception = await Assert.ThrowsAsync<VisionException>(() =>
            device.SetParameterAsync(CameraParameter.Exposure, new CameraParameterValue(5)).AsTask());

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static CameraDescriptor CreateDescriptor()
    {
        var capabilities = new CameraCapabilities(
            [new ImageSize(640, 480), new ImageSize(1920, 1080)],
            [30, 60],
            exposure: new CameraParameterRange(1, 10, 0.5, supportsAuto: true),
            gain: new CameraParameterRange(0, 5, 0.1, supportsAuto: false));

        return new CameraDescriptor("camera-1", "Test Camera", "Test", "Virtual", capabilities);
    }

    private sealed class TestCameraDevice(CameraDescriptor descriptor) : CameraDeviceBase(descriptor)
    {
        public int ConnectCount { get; private set; }

        public int DisconnectCount { get; private set; }

        public int SetParameterCount { get; private set; }

        public Exception? ConnectError { get; init; }

        public Exception? ParameterError { get; init; }

        protected override ValueTask OnConnectAsync(CancellationToken cancellationToken)
        {
            ConnectCount++;
            return ConnectError is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(ConnectError);
        }

        protected override ValueTask OnDisconnectAsync(CancellationToken cancellationToken)
        {
            DisconnectCount++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnSetParameterAsync(
            CameraParameter parameter,
            CameraParameterValue value,
            CancellationToken cancellationToken)
        {
            SetParameterCount++;
            return ParameterError is null
                ? ValueTask.CompletedTask
                : ValueTask.FromException(ParameterError);
        }
    }

    private sealed class TestCameraProvider(CameraDescriptor descriptor) : ICameraDeviceProvider
    {
        public int EnumerateCount { get; private set; }

        public int OpenCount { get; private set; }

        public ValueTask<IReadOnlyList<CameraDescriptor>> EnumerateAsync(
            CancellationToken cancellationToken = default)
        {
            EnumerateCount++;
            return ValueTask.FromResult<IReadOnlyList<CameraDescriptor>>([descriptor]);
        }

        public ValueTask<ICameraDevice> OpenAsync(
            CameraDescriptor requestedDescriptor,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            if (requestedDescriptor.Id != descriptor.Id)
            {
                throw new VisionException(VisionErrorCode.InvalidInput, "Unknown camera descriptor.");
            }

            return ValueTask.FromResult<ICameraDevice>(new TestCameraDevice(descriptor));
        }
    }
}
