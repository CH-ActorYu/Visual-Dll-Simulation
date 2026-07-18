using System.Buffers;
using Visual.Abstractions.Contracts;
using Visual.Image.Internal;

namespace Visual.Image.Contracts;

public sealed class ImageFrameFactory
{
    private readonly ImageMemoryPool _memoryPool;

    public ImageFrameFactory(ImageMemoryPool? memoryPool = null)
    {
        _memoryPool = memoryPool ?? new ImageMemoryPool();
    }

    public IImageLease Rent(
        int width,
        int height,
        PixelFormat format,
        FrameInfo? info = null,
        Action<Memory<byte>>? initialize = null,
        int? stride = null)
    {
        var size = new ImageSize(width, height);
        var actualInfo = info ?? new FrameInfo(0, DateTime.UtcNow, null, size);
        EnsureMatchingSize(actualInfo, size);

        var actualStride = stride ?? FrameLayout.GetMinimumStride(size, format);
        var requiredLength = FrameLayout.GetRequiredLength(size, format, actualStride);
        var owner = _memoryPool.Rent(requiredLength);

        try
        {
            var memory = owner.Memory[..requiredLength];
            initialize?.Invoke(memory);
            return CreateLease(actualInfo, format, actualStride, memory, owner.Dispose);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    public IImageLease FromExternal(
        Memory<byte> data,
        FrameInfo info,
        PixelFormat format,
        int stride,
        Action? release = null)
    {
        if (info is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame metadata is required.");
        }
        var requiredLength = FrameLayout.GetRequiredLength(info.Size, format, stride);
        if (data.Length < requiredLength)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"External frame data length {data.Length} is smaller than {requiredLength}.");
        }

        return CreateLease(info, format, stride, data[..requiredLength], release);
    }

    public IImageLease Clone(ImageFrame source, FrameInfo? info = null)
    {
        if (source is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A source frame is required for cloning.");
        }

        return Rent(
            source.Info.Size.Width,
            source.Info.Size.Height,
            source.Format,
            info ?? source.Info,
            memory => source.Data.Span.CopyTo(memory.Span),
            source.Stride);
    }

    private static IImageLease CreateLease(
        FrameInfo info,
        PixelFormat format,
        int stride,
        Memory<byte> memory,
        Action? release)
    {
        var memoryManager = new LeaseMemoryManager(memory);
        var frame = new ImageFrame(info, format, stride, memoryManager.Memory);
        var state = new ImageLeaseState(frame, memoryManager, release);
        return new ImageLease(state);
    }

    private static void EnsureMatchingSize(FrameInfo info, ImageSize requestedSize)
    {
        if (info.Size != requestedSize)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"Frame metadata size {info.Size.Width}x{info.Size.Height} does not match requested size {requestedSize.Width}x{requestedSize.Height}.");
        }
    }
}
