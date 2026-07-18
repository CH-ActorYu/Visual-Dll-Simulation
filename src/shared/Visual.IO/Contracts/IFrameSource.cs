using Visual.Image.Contracts;

namespace Visual.IO.Contracts;

public interface IFrameSource : IAsyncDisposable
{
    string SourceId { get; }

    int Width { get; }

    int Height { get; }

    double Fps { get; }

    bool IsRunning { get; }

    FrameSourceState State { get; }

    long DroppedCount { get; }

    ValueTask OpenAsync(CancellationToken cancellationToken = default);

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<IImageLease> ReadFramesAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
