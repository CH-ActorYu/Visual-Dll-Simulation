using Visual.Image.Contracts;

namespace Visual.Image.Internal;

internal sealed class ImageLease : IImageLease
{
    private readonly ImageLeaseState _state;
    private int _isReleased;

    public ImageLease(ImageLeaseState state)
    {
        _state = state;
    }

    public ImageFrame Frame => IsReleased
        ? throw new ObjectDisposedException(nameof(IImageLease))
        : _state.Frame;

    public bool IsReleased => Volatile.Read(ref _isReleased) != 0;

    public IImageLease Retain()
    {
        if (IsReleased)
        {
            throw new ObjectDisposedException(nameof(IImageLease));
        }

        _state.Retain();
        return new ImageLease(_state);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isReleased, 1) == 0)
        {
            _state.Release();
        }
    }
}
