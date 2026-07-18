using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Image.Tests;

public sealed class ImageViewTests
{
    [Fact]
    public void Crop_should_create_a_stride_aware_zero_copy_view()
    {
        var pixels = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        var factory = new ImageFrameFactory();
        using var lease = factory.FromExternal(
            pixels,
            new FrameInfo(0, DateTime.UtcNow, "test", new ImageSize(4, 4)),
            PixelFormat.Gray8,
            4);

        var view = new ImageView(lease).Crop(new RoiRect(1, 1, 2, 2));

        Assert.Equal(5, view.Offset);
        Assert.Equal(2, view.Width);
        Assert.Equal(2, view.Height);
        Assert.Equal(4, view.Stride);
        Assert.Equal(6, view.Data.Length);
        Assert.Equal(5, view.Data.Span[0]);
        Assert.Equal(9, view.Data.Span[4]);

        pixels[5] = 100;
        Assert.Equal(100, view.Data.Span[0]);
    }

    [Fact]
    public void View_should_be_invalid_after_owner_lease_is_released()
    {
        var factory = new ImageFrameFactory();
        var lease = factory.Rent(2, 2, PixelFormat.Gray8);
        var view = new ImageView(lease);

        lease.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = view.Data);
    }

    [Fact]
    public void Crop_should_reject_a_fully_outside_roi()
    {
        var factory = new ImageFrameFactory();
        using var lease = factory.Rent(4, 4, PixelFormat.Gray8);
        var view = new ImageView(lease);

        var exception = Assert.Throws<VisionException>(() => view.Crop(new RoiRect(10, 10, 2, 2)));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }
}
