using System.Buffers;
using Visual.Abstractions.Contracts;
using Visual.Image.Internal;

namespace Visual.Image.Contracts;

public sealed class ImageMemoryPool
{
    private readonly ArrayPool<byte> _arrayPool;
    private long _outstanding;
    private long _peakOutstanding;
    private long _totalRented;
    private long _totalReturned;

    public ImageMemoryPool()
        : this(ArrayPool<byte>.Shared)
    {
    }

    internal ImageMemoryPool(ArrayPool<byte> arrayPool)
    {
        _arrayPool = arrayPool ?? throw new ArgumentNullException(nameof(arrayPool));
    }

    public IMemoryOwner<byte> Rent(int minimumLength)
    {
        if (minimumLength <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "The requested buffer length must be positive.");
        }

        var buffer = _arrayPool.Rent(minimumLength);
        Interlocked.Increment(ref _totalRented);
        var outstanding = Interlocked.Increment(ref _outstanding);
        UpdatePeak(outstanding);

        return new PooledMemoryOwner(this, buffer, minimumLength);
    }

    public void Return(IMemoryOwner<byte> buffer)
    {
        if (buffer is not PooledMemoryOwner owner || !ReferenceEquals(owner.Pool, this))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "The buffer was not rented from this image memory pool.");
        }

        owner.Dispose();
    }

    public ImageMemoryPoolStatistics GetStatistics() => new(
        Interlocked.Read(ref _totalRented),
        Interlocked.Read(ref _totalReturned),
        Interlocked.Read(ref _outstanding),
        Interlocked.Read(ref _peakOutstanding));

    internal void Return(byte[] buffer)
    {
        _arrayPool.Return(buffer);
        Interlocked.Increment(ref _totalReturned);
        Interlocked.Decrement(ref _outstanding);
    }

    private void UpdatePeak(long outstanding)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _peakOutstanding);
            if (outstanding <= current || Interlocked.CompareExchange(ref _peakOutstanding, outstanding, current) == current)
            {
                return;
            }
        }
    }
}
