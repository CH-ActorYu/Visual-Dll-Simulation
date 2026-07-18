namespace Visual.Image.Contracts;

public interface IImageLease : IDisposable
{
    ImageFrame Frame { get; }

    bool IsReleased { get; }

    IImageLease Retain();
}
