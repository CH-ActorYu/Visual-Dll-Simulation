using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Tests;

public sealed class OpenCvImageAdapterTests
{
    [Fact]
    public void Borrowed_mat_should_wrap_source_memory_without_copying()
    {
        var bytes = new byte[4];
        using var source = new ImageFrameFactory().FromExternal(
            bytes,
            new FrameInfo(0, DateTime.UtcNow, "external", new ImageSize(2, 2)),
            PixelFormat.Gray8,
            2);
        var adapter = new OpenCvImageAdapter();

        using var borrowed = adapter.ToMat(source.Frame);
        bytes[0] = 123;

        Assert.Equal(123, borrowed.Mat.At<byte>(0, 0));
    }

    [Fact]
    public void Mat_conversion_should_copy_into_an_independent_owned_lease()
    {
        using var mat = new Mat(new Size(2, 2), MatType.CV_8UC1, new Scalar(50));
        var info = new FrameInfo(1, DateTime.UtcNow, "mat", new ImageSize(2, 2));
        var adapter = new OpenCvImageAdapter();

        using var lease = adapter.FromMat(mat, info);
        mat.Set(0, 0, (byte)200);

        Assert.Equal(50, lease.Frame.Data.Span[0]);
        Assert.False(lease.IsReleased);
    }
}
