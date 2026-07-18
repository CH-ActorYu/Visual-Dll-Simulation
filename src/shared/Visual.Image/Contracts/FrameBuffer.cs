using Visual.Abstractions.Contracts;

namespace Visual.Image.Contracts;

public sealed class FrameBuffer : IDisposable
{
    public const int DefaultCapacity = 3;

    private readonly object _sync = new();
    private readonly Queue<IImageLease> _frames;
    private bool _isCompleted;
    private long _droppedCount;

    public FrameBuffer(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame buffer capacity must be positive.");
        }

        Capacity = capacity;
        _frames = new Queue<IImageLease>(capacity);
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _frames.Count;
            }
        }
    }

    public long DroppedCount
    {
        get
        {
            lock (_sync)
            {
                return _droppedCount;
            }
        }
    }

    public bool IsCompleted
    {
        get
        {
            lock (_sync)
            {
                return _isCompleted;
            }
        }
    }

    public bool TryWrite(IImageLease lease)
    {
        if (lease is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "An image lease is required.");
        }
        if (lease.IsReleased)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A released image lease cannot be written to the buffer.");
        }

        IImageLease? dropped = null;
        lock (_sync)
        {
            if (_isCompleted)
            {
                return false;
            }

            if (_frames.Count == Capacity)
            {
                dropped = _frames.Dequeue();
                _droppedCount++;
            }

            _frames.Enqueue(lease);
        }

        dropped?.Dispose();
        return true;
    }

    public IImageLease? TryReadLatest()
    {
        List<IImageLease>? dropped = null;
        IImageLease? latest;

        lock (_sync)
        {
            if (_frames.Count == 0)
            {
                return null;
            }

            if (_frames.Count > 1)
            {
                dropped = new List<IImageLease>(_frames.Count - 1);
                while (_frames.Count > 1)
                {
                    dropped.Add(_frames.Dequeue());
                }

                _droppedCount += dropped.Count;
            }

            latest = _frames.Dequeue();
        }

        DisposeAll(dropped);
        return latest;
    }

    public void Complete()
    {
        List<IImageLease>? remaining = null;
        lock (_sync)
        {
            if (_isCompleted)
            {
                return;
            }

            _isCompleted = true;
            if (_frames.Count > 0)
            {
                remaining = [.. _frames];
                _frames.Clear();
            }
        }

        DisposeAll(remaining);
    }

    public void Dispose() => Complete();

    private static void DisposeAll(IEnumerable<IImageLease>? leases)
    {
        if (leases is null)
        {
            return;
        }

        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }
}
