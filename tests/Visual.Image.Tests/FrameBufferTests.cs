using Visual.Image.Contracts;

namespace Visual.Image.Tests;

public sealed class FrameBufferTests
{
    [Fact]
    public void Default_capacity_should_be_three()
    {
        using var buffer = new FrameBuffer();

        Assert.Equal(3, buffer.Capacity);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Full_buffer_should_drop_and_release_oldest_frame()
    {
        var factory = new ImageFrameFactory();
        using var buffer = new FrameBuffer(3);
        var first = CreateLease(factory, 1);
        var second = CreateLease(factory, 2);
        var third = CreateLease(factory, 3);
        var fourth = CreateLease(factory, 4);

        Assert.True(buffer.TryWrite(first));
        Assert.True(buffer.TryWrite(second));
        Assert.True(buffer.TryWrite(third));
        Assert.True(buffer.TryWrite(fourth));

        Assert.True(first.IsReleased);
        Assert.False(second.IsReleased);
        Assert.Equal(3, buffer.Count);
        Assert.Equal(1, buffer.DroppedCount);

        using var latest = buffer.TryReadLatest();
        Assert.NotNull(latest);
        Assert.Equal(4, latest.Frame.Data.Span[0]);
        Assert.True(second.IsReleased);
        Assert.True(third.IsReleased);
        Assert.Equal(3, buffer.DroppedCount);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Complete_should_release_queued_frames_and_be_idempotent()
    {
        var factory = new ImageFrameFactory();
        var buffer = new FrameBuffer();
        var first = CreateLease(factory, 1);
        var second = CreateLease(factory, 2);
        buffer.TryWrite(first);
        buffer.TryWrite(second);

        buffer.Complete();
        buffer.Complete();

        Assert.True(buffer.IsCompleted);
        Assert.True(first.IsReleased);
        Assert.True(second.IsReleased);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Failed_write_after_completion_should_leave_ownership_with_caller()
    {
        var factory = new ImageFrameFactory();
        using var buffer = new FrameBuffer();
        buffer.Complete();
        using var lease = CreateLease(factory, 9);

        var accepted = buffer.TryWrite(lease);

        Assert.False(accepted);
        Assert.False(lease.IsReleased);
    }

    private static IImageLease CreateLease(ImageFrameFactory factory, byte value) =>
        factory.Rent(1, 1, PixelFormat.Gray8, initialize: memory => memory.Span[0] = value);
}
