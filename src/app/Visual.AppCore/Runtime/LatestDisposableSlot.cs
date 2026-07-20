namespace Visual.AppCore.Runtime;

public sealed class LatestDisposableSlot<T> : IDisposable
    where T : class, IDisposable
{
    private readonly object _sync = new();
    private T? _value;
    private bool _isDisposed;

    public bool HasValue
    {
        get
        {
            lock (_sync)
            {
                return _value is not null;
            }
        }
    }

    public void Publish(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        T? stale;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            stale = _value;
            _value = value;
        }

        stale?.Dispose();
    }

    public T? Take()
    {
        lock (_sync)
        {
            var value = _value;
            _value = null;
            return value;
        }
    }

    public void Dispose()
    {
        T? pending;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            pending = _value;
            _value = null;
        }

        pending?.Dispose();
    }
}
