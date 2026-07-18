using System.Buffers;
using Visual.Image.Contracts;

namespace Visual.Image.Internal;

internal sealed class PooledMemoryOwner : IMemoryOwner<byte>
{
    private byte[]? _buffer;
    private readonly int _length;

    public PooledMemoryOwner(ImageMemoryPool pool, byte[] buffer, int length)
    {
        Pool = pool;
        _buffer = buffer;
        _length = length;
    }

    internal ImageMemoryPool Pool { get; }

    public Memory<byte> Memory => (_buffer ?? throw new ObjectDisposedException(nameof(PooledMemoryOwner)))
        .AsMemory(0, _length);

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
        {
            Pool.Return(buffer);
        }
    }
}
