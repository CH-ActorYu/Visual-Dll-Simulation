using Visual.Abstractions.Contracts;

namespace Visual.Image.Tests;

public sealed class AbstractionsValueObjectTests
{
    [Fact]
    public void Roi_clamp_should_intersect_partially_outside_bounds()
    {
        var roi = new RoiRect(-5, 8, 20, 10);

        var actual = roi.ClampTo(12, 12);

        Assert.Equal(new RoiRect(0, 8, 12, 4), actual);
        Assert.Equal(48, actual.Area);
    }

    [Fact]
    public void Roi_clamp_should_reject_fully_outside_bounds()
    {
        var exception = Assert.Throws<VisionException>(() => new RoiRect(20, 20, 5, 5).ClampTo(10, 10));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Roi_contains_should_use_half_open_pixel_bounds()
    {
        var roi = new RoiRect(10, 20, 5, 3);

        Assert.True(roi.Contains(new Point2D(10, 20)));
        Assert.True(roi.Contains(new Point2D(14.999, 22.999)));
        Assert.False(roi.Contains(new Point2D(15, 21)));
        Assert.False(roi.Contains(new Point2D(12, 23)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Image_size_should_require_positive_dimensions(int width, int height)
    {
        var exception = Assert.Throws<VisionException>(() => new ImageSize(width, height));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Point_should_reject_non_finite_coordinates()
    {
        var exception = Assert.Throws<VisionException>(() => new Point2D(double.NaN, 0));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Vision_exception_should_preserve_structured_error_code()
    {
        var exception = new VisionException(VisionErrorCode.DeviceLost, "Camera disconnected.");

        Assert.Equal(VisionErrorCode.DeviceLost, exception.ErrorCode);
        Assert.Equal("Camera disconnected.", exception.Message);
    }
}
