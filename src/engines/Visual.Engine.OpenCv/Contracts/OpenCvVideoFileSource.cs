using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvVideoFileSource : OpenCvFrameSourceBase
{
    private readonly string _path;
    private readonly bool _loopPlayback;
    private readonly PlaybackControl _playback = new();
    private VideoCapture? _capture;

    public OpenCvVideoFileSource(
        string sourceId,
        string path,
        int bufferCapacity = 3,
        bool loopPlayback = false)
        : base(sourceId, bufferCapacity)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? throw OpenCvErrors.Invalid("A video file path is required.")
            : Path.GetFullPath(path);
        _loopPlayback = loopPlayback;
    }

    public bool IsPaused => _playback.IsPaused;

    public bool LoopPlayback => _loopPlayback;

    public ValueTask PauseAsync()
    {
        _playback.Pause();
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeAsync()
    {
        _playback.Resume();
        return ValueTask.CompletedTask;
    }

    public ValueTask StepAsync()
    {
        _playback.Step();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnOpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_path))
        {
            throw OpenCvErrors.Decode($"Video file '{_path}' does not exist.");
        }

        try
        {
            var capture = new VideoCapture(_path);
            if (!capture.IsOpened())
            {
                capture.Dispose();
                throw OpenCvErrors.Decode($"Video file '{_path}' could not be opened.");
            }

            _capture = capture;
            Width = checked((int)capture.FrameWidth);
            Height = checked((int)capture.FrameHeight);
            Fps = NormalizeFps(capture.Fps);
            if (Width <= 0 || Height <= 0)
            {
                throw OpenCvErrors.Decode("Video metadata contains invalid dimensions.");
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception exception)
        {
            _capture?.Dispose();
            _capture = null;
            throw exception is VisionException ? exception : OpenCvErrors.Decode("Video open failed.", exception);
        }
    }

    protected override async Task RunCaptureAsync(CancellationToken cancellationToken)
    {
        var capture = _capture ?? throw OpenCvErrors.Decode("Video source is not open.");
        var delay = TimeSpan.FromSeconds(1 / Fps);
        using var frame = new Mat();
        while (!cancellationToken.IsCancellationRequested)
        {
            await _playback.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!capture.Read(frame) || frame.Empty())
            {
                if (!_loopPlayback)
                {
                    return;
                }

                capture.PosFrames = 0;
                if (!capture.Read(frame) || frame.Empty())
                {
                    return;
                }
            }

            Publish(frame);
            if (!_playback.IsPaused)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected override ValueTask OnCloseAsync(CancellationToken cancellationToken)
    {
        _capture?.Dispose();
        _capture = null;
        return ValueTask.CompletedTask;
    }

}
