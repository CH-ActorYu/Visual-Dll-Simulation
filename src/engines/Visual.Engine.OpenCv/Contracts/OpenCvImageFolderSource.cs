using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvImageFolderSource : OpenCvFrameSourceBase
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
    };

    private readonly string _folder;
    private readonly double _requestedFps;
    private readonly PlaybackControl _playback = new();
    private string[] _files = [];

    public OpenCvImageFolderSource(
        string sourceId,
        string folder,
        double fps = 10,
        int bufferCapacity = 3)
        : base(sourceId, bufferCapacity)
    {
        _folder = string.IsNullOrWhiteSpace(folder)
            ? throw OpenCvErrors.Invalid("An image folder is required.")
            : Path.GetFullPath(folder);
        _requestedFps = NormalizeFps(fps, 10);
    }

    public bool IsPaused => _playback.IsPaused;

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
        if (!Directory.Exists(_folder))
        {
            throw OpenCvErrors.Decode($"Image folder '{_folder}' does not exist.");
        }

        _files = Directory.EnumerateFiles(_folder)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (_files.Length == 0)
        {
            throw OpenCvErrors.Decode($"Image folder '{_folder}' contains no supported images.");
        }

        using var first = Cv2.ImRead(_files[0], ImreadModes.Unchanged);
        if (first.Empty())
        {
            throw OpenCvErrors.Decode($"Image '{_files[0]}' could not be decoded.");
        }

        Width = first.Cols;
        Height = first.Rows;
        Fps = _requestedFps;
        return ValueTask.CompletedTask;
    }

    protected override async Task RunCaptureAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1 / Fps);
        foreach (var file in _files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _playback.WaitAsync(cancellationToken).ConfigureAwait(false);
            using var frame = Cv2.ImRead(file, ImreadModes.Unchanged);
            if (frame.Empty())
            {
                throw OpenCvErrors.Decode($"Image '{file}' could not be decoded.");
            }

            Publish(frame);
            if (!_playback.IsPaused)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

}
