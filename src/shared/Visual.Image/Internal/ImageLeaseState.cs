using Visual.Image.Contracts;

namespace Visual.Image.Internal;

internal sealed class ImageLeaseState
{
    private readonly LeaseMemoryManager _memoryManager;
    private Action? _release;
    private int _referenceCount = 1;

    public ImageLeaseState(ImageFrame frame, LeaseMemoryManager memoryManager, Action? release)
    {
        Frame = frame;
        _memoryManager = memoryManager;
        _release = release;
    }

    public ImageFrame Frame { get; }

    public void Retain()
    {
        while (true)
        {
            var current = Volatile.Read(ref _referenceCount);
            if (current == 0)
            {
                throw new ObjectDisposedException(nameof(IImageLease));
            }

            if (Interlocked.CompareExchange(ref _referenceCount, checked(current + 1), current) == current)
            {
                return;
            }
        }
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _referenceCount) != 0)
        {
            return;
        }

        _memoryManager.Invalidate();
        Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
