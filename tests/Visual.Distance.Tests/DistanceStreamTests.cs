using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Image.Contracts;

namespace Visual.Distance.Tests;

public sealed class DistanceStreamTests
{
    [Fact]
    public async Task Uncalibrated_stream_should_emit_status_and_clean_up_source()
    {
        var pool = new ImageMemoryPool();
        var source = new TestFrameSource(new ImageFrameFactory(pool));
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, _) => null))
            .Build();
        var stream = service.MeasureStreamAsync(
            source,
            [new TargetRegion("target", new RoiRect(0, 0, 30, 30))]);
        var frames = stream.GetAsyncEnumerator();
        try
        {
            Assert.True(await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            var result = Assert.Single(frames.Current);
            Assert.Equal(VisionResultStatus.NotCalibrated, result.Status);
            Assert.Null(result.Distance);
        }
        finally
        {
            await frames.DisposeAsync();
        }

        Assert.Equal(1, source.OpenCount);
        Assert.Equal(1, source.CloseCount);
        Assert.Equal(0, pool.GetStatistics().Outstanding);
        await source.DisposeAsync();
    }

    [Fact]
    public async Task Stream_should_convert_algorithm_failure_to_failed_result()
    {
        var pool = new ImageMemoryPool();
        var source = new TestFrameSource(new ImageFrameFactory(pool));
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, _) => throw new InvalidOperationException("native failure")))
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        var frames = service.MeasureStreamAsync(
                source,
                [new TargetRegion("target", new RoiRect(0, 0, 30, 30))])
            .GetAsyncEnumerator();
        try
        {
            Assert.True(await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            var result = Assert.Single(frames.Current);
            Assert.Equal(VisionResultStatus.Failed, result.Status);
            Assert.Equal(VisionErrorCode.ModuleSpecific, result.ErrorCode);
        }
        finally
        {
            await frames.DisposeAsync();
        }

        Assert.Equal(0, pool.GetStatistics().Outstanding);
        await source.DisposeAsync();
    }

    [Fact]
    public async Task Stream_should_recover_after_calibration_is_supplied()
    {
        var pool = new ImageMemoryPool();
        var source = new ControllableFrameSource(new ImageFrameFactory(pool));
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, roi) => DistanceFixtures.Candidate(roi)))
            .Build();
        var frames = service.MeasureStreamAsync(
                source,
                [new TargetRegion("target", new RoiRect(0, 0, 30, 30))])
            .GetAsyncEnumerator();
        try
        {
            var firstMove = frames.MoveNextAsync().AsTask();
            await WaitUntilAsync(() => source.IsRunning);
            Assert.True(source.Publish());
            Assert.True(await firstMove.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(VisionResultStatus.NotCalibrated, Assert.Single(frames.Current).Status);

            service.SetCalibration(DistanceFixtures.Calibration());
            var secondMove = frames.MoveNextAsync().AsTask();
            Assert.True(source.Publish());
            Assert.True(await secondMove.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(VisionResultStatus.Valid, Assert.Single(frames.Current).Status);
        }
        finally
        {
            await frames.DisposeAsync();
        }

        Assert.Equal(1, source.CloseCount);
        Assert.Equal(0, pool.GetStatistics().Outstanding);
        await source.DisposeAsync();
    }

    [Fact]
    public async Task Cancellation_should_complete_stream_and_release_buffered_leases()
    {
        var pool = new ImageMemoryPool();
        var source = new TestFrameSource(
            new ImageFrameFactory(pool),
            frameCount: 5,
            keepAlive: true,
            bufferCapacity: 2);
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, roi) => DistanceFixtures.Candidate(roi)))
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var cancellation = new CancellationTokenSource();
        var frames = service.MeasureStreamAsync(
                source,
                [new TargetRegion("target", new RoiRect(0, 0, 30, 30))],
                cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token);

        Assert.True(await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        cancellation.Cancel();
        Assert.False(await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        await frames.DisposeAsync();

        Assert.True(source.DroppedCount > 0);
        Assert.Equal(0, pool.GetStatistics().Outstanding);
        Assert.Equal(1, source.CloseCount);
        await source.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
