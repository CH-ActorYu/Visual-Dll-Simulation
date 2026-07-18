using System.Buffers;

namespace Visual.Image.Internal;

internal sealed class LeaseMemoryManager : MemoryManager<byte>
{
    private Memory<byte> _memory;
    private int _isReleased;

    public LeaseMemoryManager(Memory<byte> memory)
    {
        _memory = memory;
    }

    public override Span<byte> GetSpan()
    {
        ThrowIfReleased();
        return _memory.Span;
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ThrowIfReleased();
        if ((uint)elementIndex > (uint)_memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        return _memory[elementIndex..].Pin();
    }

    public override void Unpin()
    {
    }

    internal void Invalidate() => Dispose(true);

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _isReleased, 1) == 0)
        {
            _memory = Memory<byte>.Empty;
        }
    }

    private void ThrowIfReleased()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isReleased) != 0, this);
    }
}
