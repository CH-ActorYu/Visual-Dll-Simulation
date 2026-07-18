using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.IO.Contracts;
using Visual.VisionBase.Contracts;

namespace Visual.VisionBase.Tests;

public sealed class VisionRuntimeTests
{
    [Fact]
    public async Task Runtime_should_process_and_release_owned_frame_leases()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        var source = new TestFrameSource();
        var processed = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnostics = new VisionDiagnostics();
        var processor = new DelegateProcessor((frame, _) =>
        {
            processed.TrySetResult(frame.Info.FrameIndex);
            return ValueTask.CompletedTask;
        });
        var runtime = new VisionRuntime(source, processor, diagnostics, "test-engine");

        await runtime.StartAsync();
        Assert.Equal(VisionRuntimeState.Running, runtime.State);
        Assert.True(source.Publish(CreateLease(factory, 7)));
        Assert.Equal(7, await processed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await WaitUntilAsync(() => pool.GetStatistics().Outstanding == 0);

        await runtime.StopAsync();
        Assert.Equal(VisionRuntimeState.Stopped, runtime.State);
        Assert.Contains(diagnostics.Snapshot(), record =>
            record.Kind == VisionDiagnosticKind.FrameProcessed && record.FrameIndex == 7);
        Assert.Contains(diagnostics.Snapshot(), record => record.Kind == VisionDiagnosticKind.RuntimeStopped);

        await runtime.DisposeAsync();
        Assert.Equal(VisionRuntimeState.Disposed, runtime.State);
        Assert.Equal(1, source.OpenCount);
        Assert.Equal(1, source.CloseCount);
    }

    [Fact]
    public async Task Runtime_should_normalize_processor_failure_and_release_frame()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        var source = new TestFrameSource();
        var diagnostics = new VisionDiagnostics();
        var runtime = new VisionRuntime(
            source,
            new DelegateProcessor((_, _) => throw new InvalidOperationException("algorithm failure")),
            diagnostics,
            "test-engine");

        await runtime.StartAsync();
        Assert.True(source.Publish(CreateLease(factory, 1)));
        var exception = await Assert.ThrowsAsync<VisionException>(async () =>
            await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
        Assert.Equal(VisionRuntimeState.Failed, runtime.State);
        Assert.Equal(VisionErrorCode.ModuleSpecific, runtime.LastError?.ErrorCode);
        await WaitUntilAsync(() => pool.GetStatistics().Outstanding == 0);
        Assert.Contains(diagnostics.Snapshot(), record => record.Kind == VisionDiagnosticKind.EngineFailed);

        var stopException = await Assert.ThrowsAsync<VisionException>(async () => await runtime.StopAsync());
        Assert.Equal(VisionErrorCode.ModuleSpecific, stopException.ErrorCode);
        Assert.Equal(VisionRuntimeState.Stopped, runtime.State);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task Cancel_current_work_should_stop_processing_and_release_buffered_frames()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        var source = new TestFrameSource(bufferCapacity: 2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processor = new DelegateProcessor(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        });
        var runtime = new VisionRuntime(source, processor);

        await runtime.StartAsync();
        Assert.True(source.Publish(CreateLease(factory, 1)));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(source.Publish(CreateLease(factory, 2)));
        Assert.True(source.Publish(CreateLease(factory, 3)));

        runtime.CancelCurrentWork();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        await runtime.StopAsync();
        await WaitUntilAsync(() => pool.GetStatistics().Outstanding == 0);
        await runtime.DisposeAsync();
    }

    private static IImageLease CreateLease(ImageFrameFactory factory, long frameIndex) => factory.Rent(
        8,
        8,
        PixelFormat.Gray8,
        new FrameInfo(frameIndex, DateTime.UtcNow, "runtime-source", new ImageSize(8, 8)));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class DelegateProcessor(
        Func<ImageFrame, CancellationToken, ValueTask> process) : IFrameProcessor
    {
        public ValueTask ProcessAsync(ImageFrame frame, CancellationToken cancellationToken = default) =>
            process(frame, cancellationToken);
    }

    private sealed class TestFrameSource(int bufferCapacity = 3)
        : BufferedFrameSourceBase("runtime-source", bufferCapacity)
    {
        public int OpenCount { get; private set; }

        public int CloseCount { get; private set; }

        public bool Publish(IImageLease lease) => TryPublishFrame(lease);

        protected override ValueTask OnOpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            Width = 8;
            Height = 8;
            Fps = 30;
            return ValueTask.CompletedTask;
        }

        protected override Task RunCaptureAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override ValueTask OnCloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return ValueTask.CompletedTask;
        }
    }
}
