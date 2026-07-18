using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Image.Tests;

public sealed class ImageFrameAndLeaseTests
{
    [Fact]
    public void Rent_should_initialize_frame_and_return_memory_to_pool_once()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        var info = CreateInfo(7, 4, 3);

        var lease = factory.Rent(4, 3, PixelFormat.Gray8, info, memory => memory.Span.Fill(42));

        Assert.Equal(info, lease.Frame.Info);
        Assert.Equal(12, lease.Frame.Data.Length);
        Assert.All(lease.Frame.Data.ToArray(), value => Assert.Equal(42, value));
        Assert.Equal(1, pool.GetStatistics().Outstanding);

        lease.Dispose();
        lease.Dispose();

        var statistics = pool.GetStatistics();
        Assert.Equal(1, statistics.TotalRented);
        Assert.Equal(1, statistics.TotalReturned);
        Assert.Equal(0, statistics.Outstanding);
    }

    [Fact]
    public void Retain_should_keep_memory_alive_until_last_handle_is_released()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        var lease = factory.Rent(2, 2, PixelFormat.Gray8);
        var retained = lease.Retain();
        var borrowedData = lease.Frame.Data;

        lease.Dispose();

        Assert.True(lease.IsReleased);
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Frame);
        Assert.Equal(1, pool.GetStatistics().Outstanding);
        Assert.Equal(4, borrowedData.Span.Length);

        retained.Dispose();

        Assert.Equal(0, pool.GetStatistics().Outstanding);
        Assert.Throws<ObjectDisposedException>(() => _ = borrowedData.Span.Length);
    }

    [Fact]
    public void Clone_should_own_an_independent_copy()
    {
        var external = new byte[] { 1, 2, 3, 4 };
        var factory = new ImageFrameFactory();
        using var source = factory.FromExternal(external, CreateInfo(1, 2, 2), PixelFormat.Gray8, 2);
        using var clone = factory.Clone(source.Frame, CreateInfo(2, 2, 2));

        external[0] = 99;

        Assert.Equal(99, source.Frame.Data.Span[0]);
        Assert.Equal(1, clone.Frame.Data.Span[0]);
        Assert.Equal(2, clone.Frame.Info.FrameIndex);
    }

    [Fact]
    public void From_external_should_not_copy_and_should_release_once()
    {
        var releases = 0;
        var external = new byte[4];
        var factory = new ImageFrameFactory();
        var lease = factory.FromExternal(
            external,
            CreateInfo(3, 2, 2),
            PixelFormat.Gray8,
            2,
            () => releases++);

        external[2] = 17;
        Assert.Equal(17, lease.Frame.Data.Span[2]);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, releases);
    }

    [Fact]
    public void Factory_should_return_pool_memory_when_initializer_throws()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);

        Assert.Throws<InvalidOperationException>(() =>
            factory.Rent(2, 2, PixelFormat.Gray8, initialize: _ => throw new InvalidOperationException("boom")));

        Assert.Equal(0, pool.GetStatistics().Outstanding);
        Assert.Equal(pool.GetStatistics().TotalRented, pool.GetStatistics().TotalReturned);
    }

    [Fact]
    public void Frame_should_reject_invalid_stride_and_short_data()
    {
        var info = CreateInfo(0, 3, 2);

        var strideException = Assert.Throws<VisionException>(() =>
            new ImageFrame(info, PixelFormat.Bgr24, 8, new byte[18]));
        var lengthException = Assert.Throws<VisionException>(() =>
            new ImageFrame(info, PixelFormat.Gray8, 3, new byte[5]));

        Assert.Equal(VisionErrorCode.InvalidInput, strideException.ErrorCode);
        Assert.Equal(VisionErrorCode.InvalidInput, lengthException.ErrorCode);
    }

    [Fact]
    public void Factory_should_report_layout_overflow_as_invalid_input()
    {
        var factory = new ImageFrameFactory();

        var exception = Assert.Throws<VisionException>(() =>
            factory.Rent(int.MaxValue, 2, PixelFormat.Bgra32));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
        Assert.IsType<OverflowException>(exception.InnerException);
    }

    [Fact]
    public void Frame_should_report_missing_metadata_as_invalid_input()
    {
        var exception = Assert.Throws<VisionException>(() =>
            new ImageFrame(null!, PixelFormat.Gray8, 1, new byte[1]));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    private static FrameInfo CreateInfo(long index, int width, int height) =>
        new(index, DateTime.UtcNow, "test", new ImageSize(width, height));
}
