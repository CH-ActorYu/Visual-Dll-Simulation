using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.IO.Contracts;

namespace Visual.IO.Tests;

public sealed class BufferedFrameSourceTests
{
    [Fact]
    public async Task Lifecycle_operations_should_be_idempotent()
    {
        await using var source = new TestFrameSource();

        await source.OpenAsync();
        await source.OpenAsync();
        await source.StartAsync();
        await source.StartAsync();
        await source.StopAsync();
        await source.StopAsync();
        await source.CloseAsync();
        await source.CloseAsync();

        Assert.Equal(1, source.OpenCount);
        Assert.Equal(1, source.StartCount);
        Assert.Equal(1, source.StopCount);
        Assert.Equal(1, source.CloseCount);
        Assert.Equal(FrameSourceState.Closed, source.State);
    }

    [Fact]
    public async Task Buffered_source_should_keep_latest_frame_and_release_older_frames()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        await using var source = new TestFrameSource(bufferCapacity: 3);
        await source.OpenAsync();
        await source.StartAsync();
        var leases = Enumerable.Range(1, 5).Select(value => CreateLease(factory, (byte)value)).ToArray();

        foreach (var lease in leases)
        {
            Assert.True(source.Publish(lease));
        }

        Assert.True(leases[0].IsReleased);
        Assert.True(leases[1].IsReleased);

        await using var enumerator = source.ReadFramesAsync().GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        var latest = enumerator.Current;

        Assert.Equal(5, latest.Frame.Data.Span[0]);
        Assert.Equal(4, source.DroppedCount);
        Assert.True(leases[2].IsReleased);
        Assert.True(leases[3].IsReleased);
        latest.Dispose();

        await source.StopAsync();
        Assert.Equal(0, pool.GetStatistics().Outstanding);
    }

    [Fact]
    public async Task Enumeration_cancellation_should_stop_source_and_release_queued_frames()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        await using var source = new TestFrameSource();
        await source.OpenAsync();
        await source.StartAsync();
        source.Publish(CreateLease(factory, 1));
        source.Publish(CreateLease(factory, 2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var enumerator = source.ReadFramesAsync(cancellation.Token).GetAsyncEnumerator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());

        Assert.Equal(FrameSourceState.Opened, source.State);
        Assert.Equal(0, pool.GetStatistics().Outstanding);
    }

    [Fact]
    public async Task Fatal_error_should_release_queue_and_end_stream_with_vision_exception()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        await using var source = new TestFrameSource();
        await source.OpenAsync();
        await source.StartAsync();
        source.Publish(CreateLease(factory, 1));
        source.Publish(CreateLease(factory, 2));
        source.Fail(new VisionException(VisionErrorCode.DeviceLost, "Camera disconnected."));
        Assert.True(SpinWait.SpinUntil(() => source.State == FrameSourceState.Failed, TimeSpan.FromSeconds(2)));
        await using var enumerator = source.ReadFramesAsync().GetAsyncEnumerator();

        var exception = await Assert.ThrowsAsync<VisionException>(() => enumerator.MoveNextAsync().AsTask());

        Assert.Equal(VisionErrorCode.DeviceLost, exception.ErrorCode);
        Assert.Equal(0, pool.GetStatistics().Outstanding);
    }

    [Fact]
    public async Task Unknown_capture_error_should_be_normalized_and_release_queue()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        await using var source = new TestFrameSource();
        await source.OpenAsync();
        await source.StartAsync();
        source.Publish(CreateLease(factory, 1));
        source.Fail(new InvalidOperationException("native failure"));
        Assert.True(SpinWait.SpinUntil(() => source.State == FrameSourceState.Failed, TimeSpan.FromSeconds(2)));
        await using var enumerator = source.ReadFramesAsync().GetAsyncEnumerator();

        var exception = await Assert.ThrowsAsync<VisionException>(() => enumerator.MoveNextAsync().AsTask());

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(0, pool.GetStatistics().Outstanding);
    }

    [Fact]
    public async Task Rejected_publish_should_leave_lease_with_caller()
    {
        var factory = new ImageFrameFactory();
        await using var source = new TestFrameSource();
        await source.OpenAsync();
        await source.StartAsync();
        await source.StopAsync();
        using var lease = CreateLease(factory, 1);

        Assert.False(source.Publish(lease));
        Assert.False(lease.IsReleased);
    }

    [Fact]
    public async Task Open_cancellation_should_restore_closed_state()
    {
        await using var source = new TestFrameSource { BlockOpen = true };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.OpenAsync(cancellation.Token).AsTask());

        Assert.Equal(FrameSourceState.Closed, source.State);
    }

    [Fact]
    public async Task Start_cancellation_should_restore_opened_state()
    {
        await using var source = new TestFrameSource { BlockStart = true };
        await source.OpenAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.StartAsync(cancellation.Token).AsTask());

        Assert.Equal(FrameSourceState.Opened, source.State);
        Assert.False(source.IsRunning);
    }

    private static IImageLease CreateLease(ImageFrameFactory factory, byte value) =>
        factory.Rent(1, 1, PixelFormat.Gray8, initialize: memory => memory.Span[0] = value);

    private sealed class TestFrameSource : BufferedFrameSourceBase
    {
        private readonly TaskCompletionSource<Exception?> _terminal =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestFrameSource(int bufferCapacity = FrameBuffer.DefaultCapacity)
            : base("test-source", bufferCapacity)
        {
        }

        public int OpenCount { get; private set; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int CloseCount { get; private set; }

        public bool BlockOpen { get; init; }

        public bool BlockStart { get; init; }

        public bool Publish(IImageLease lease) => TryPublishFrame(lease);

        public void Fail(Exception exception) => _terminal.TrySetResult(exception);

        protected override ValueTask OnOpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            if (BlockOpen)
            {
                return new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
            }

            Width = 1;
            Height = 1;
            Fps = 30;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnStartAsync(CancellationToken cancellationToken)
        {
            StartCount++;
            return BlockStart
                ? new ValueTask(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken))
                : ValueTask.CompletedTask;
        }

        protected override async Task RunCaptureAsync(CancellationToken cancellationToken)
        {
            var terminal = await _terminal.Task.WaitAsync(cancellationToken);
            if (terminal is not null)
            {
                throw terminal;
            }
        }

        protected override ValueTask OnStopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnCloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return ValueTask.CompletedTask;
        }
    }
}
