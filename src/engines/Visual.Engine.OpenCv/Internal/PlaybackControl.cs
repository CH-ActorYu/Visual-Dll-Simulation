namespace Visual.Engine.OpenCv.Internal;

internal sealed class PlaybackControl
{
    private readonly SemaphoreSlim _steps = new(0);
    private int _paused;

    public bool IsPaused => Volatile.Read(ref _paused) != 0;

    public void Pause()
    {
        Interlocked.Exchange(ref _paused, 1);
        while (_steps.Wait(0))
        {
        }
    }

    public void Resume()
    {
        Interlocked.Exchange(ref _paused, 0);
        _steps.Release();
    }

    public void Step()
    {
        Pause();
        _steps.Release();
    }

    public async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        while (IsPaused)
        {
            await _steps.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (IsPaused)
            {
                return;
            }
        }
    }
}
