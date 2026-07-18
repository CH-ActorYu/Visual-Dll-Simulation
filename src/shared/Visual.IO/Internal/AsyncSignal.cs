namespace Visual.IO.Internal;

internal sealed class AsyncSignal : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Set()
    {
        if (_semaphore.CurrentCount == 0)
        {
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken) => _semaphore.WaitAsync(cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
