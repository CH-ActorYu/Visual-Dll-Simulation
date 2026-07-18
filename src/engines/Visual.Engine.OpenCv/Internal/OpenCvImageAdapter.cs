using System.Buffers;
using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Internal;

internal sealed class OpenCvImageAdapter
{
    private readonly ImageFrameFactory _frameFactory;

    public OpenCvImageAdapter(ImageFrameFactory? frameFactory = null)
    {
        _frameFactory = frameFactory ?? new ImageFrameFactory();
    }

    public unsafe BorrowedMat ToMat(ImageFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var pin = frame.Data.Pin();
        try
        {
            var mat = Mat.FromPixelData(
                frame.Info.Size.Height,
                frame.Info.Size.Width,
                ToMatType(frame.Format),
                (IntPtr)pin.Pointer,
                frame.Stride);
            return new BorrowedMat(mat, pin);
        }
        catch
        {
            pin.Dispose();
            throw;
        }
    }

    public unsafe IImageLease FromMat(Mat source, FrameInfo info, PixelFormat? format = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(info);
        if (source.Empty())
        {
            throw OpenCvErrors.Invalid("Cannot convert an empty OpenCV matrix.");
        }

        var rows = source.Rows;
        var columns = source.Cols;
        var type = source.Type();
        var actualFormat = format ?? FromMatType(type);
        var bytesPerPixel = actualFormat.BytesPerPixel();
        var rowLength = checked(columns * bytesPerPixel);
        if (rows != info.Size.Height || columns != info.Size.Width)
        {
            throw OpenCvErrors.Invalid("OpenCV matrix dimensions do not match frame metadata.");
        }

        return _frameFactory.Rent(
            columns,
            rows,
            actualFormat,
            info,
            destination =>
            {
                for (var row = 0; row < rows; row++)
                {
                    var sourceRow = new ReadOnlySpan<byte>((void*)source.Ptr(row), rowLength);
                    sourceRow.CopyTo(destination.Span.Slice(row * rowLength, rowLength));
                }
            },
            rowLength);
    }

    public static MatType ToMatType(PixelFormat format) => format switch
    {
        PixelFormat.Gray8 => MatType.CV_8UC1,
        PixelFormat.Bgr24 or PixelFormat.Rgb24 => MatType.CV_8UC3,
        PixelFormat.Bgra32 => MatType.CV_8UC4,
        _ => throw OpenCvErrors.Invalid($"Unsupported pixel format {format}.")
    };

    public static PixelFormat FromMatType(MatType type)
    {
        if (type == MatType.CV_8UC1)
        {
            return PixelFormat.Gray8;
        }

        if (type == MatType.CV_8UC3)
        {
            return PixelFormat.Bgr24;
        }

        if (type == MatType.CV_8UC4)
        {
            return PixelFormat.Bgra32;
        }

        throw OpenCvErrors.Invalid($"Unsupported OpenCV matrix type {type}.");
    }
}

internal sealed class BorrowedMat(Mat mat, MemoryHandle pin) : IDisposable
{
    public Mat Mat { get; } = mat;

    public void Dispose()
    {
        Mat.Dispose();
        pin.Dispose();
    }
}
