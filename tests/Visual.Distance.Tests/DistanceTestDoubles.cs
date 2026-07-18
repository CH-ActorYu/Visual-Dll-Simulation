using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Image.Contracts;
using Visual.IO.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Tests;

internal sealed class TestDetector(
    Func<ImageFrame, RoiRect, TargetCandidate?> detect) : ITargetDetector
{
    public int CallCount { get; private set; }

    public TargetCandidate? Detect(ImageFrame frame, RoiRect roi)
    {
        CallCount++;
        return detect(frame, roi);
    }
}

internal sealed class TestTracker : ITracker
{
    private readonly TargetCandidate? _candidate;

    public TestTracker(TargetCandidate? candidate = null)
    {
        _candidate = candidate;
    }

    public int InitializeCount { get; private set; }

    public int DisposeCount { get; private set; }

    public void Initialize(ImageFrame frame, RoiRect roi) => InitializeCount++;

    public TargetCandidate? Update(ImageFrame frame) => _candidate;

    public void Dispose() => DisposeCount++;
}

internal sealed class TestEngine(
    VisionEngineInfo info,
    ITargetDetector detector,
    Func<ITracker>? trackerFactory = null) : IVisionEngine
{
    public VisionEngineInfo Info { get; } = info;

    public int DetectorCreateCount { get; private set; }

    public ITargetDetector CreateDetector(DetectionProfile profile)
    {
        DetectorCreateCount++;
        return detector;
    }

    public ITracker CreateTracker(DetectionProfile profile) =>
        trackerFactory?.Invoke() ?? new TestTracker();

    public IPreprocessOperator CreatePreprocessor(DetectionProfile profile) => throw new NotSupportedException();
}

internal sealed class TestFrameSource(
    ImageFrameFactory factory,
    int frameCount = 1,
    bool keepAlive = true,
    int bufferCapacity = 3) : BufferedFrameSourceBase("camera-1", bufferCapacity)
{
    public int OpenCount { get; private set; }

    public int CloseCount { get; private set; }

    protected override ValueTask OnOpenAsync(CancellationToken cancellationToken)
    {
        OpenCount++;
        Width = 100;
        Height = 80;
        Fps = 30;
        return ValueTask.CompletedTask;
    }

    protected override async Task RunCaptureAsync(CancellationToken cancellationToken)
    {
        for (var index = 0; index < frameCount; index++)
        {
            var lease = factory.Rent(
                100,
                80,
                PixelFormat.Gray8,
                new FrameInfo(index, DateTime.UtcNow, SourceId, new ImageSize(100, 80)));
            if (!TryPublishFrame(lease))
            {
                lease.Dispose();
            }
        }

        if (keepAlive)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    protected override ValueTask OnCloseAsync(CancellationToken cancellationToken)
    {
        CloseCount++;
        return ValueTask.CompletedTask;
    }
}

internal sealed class ControllableFrameSource(ImageFrameFactory factory)
    : BufferedFrameSourceBase("camera-1", 2)
{
    private long _frameIndex;

    public int CloseCount { get; private set; }

    public bool Publish()
    {
        var lease = factory.Rent(
            100,
            80,
            PixelFormat.Gray8,
            new FrameInfo(
                Interlocked.Increment(ref _frameIndex) - 1,
                DateTime.UtcNow,
                SourceId,
                new ImageSize(100, 80)));
        if (TryPublishFrame(lease))
        {
            return true;
        }

        lease.Dispose();
        return false;
    }

    protected override ValueTask OnOpenAsync(CancellationToken cancellationToken)
    {
        Width = 100;
        Height = 80;
        Fps = 30;
        return ValueTask.CompletedTask;
    }

    protected override Task RunCaptureAsync(CancellationToken cancellationToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

    protected override ValueTask OnCloseAsync(CancellationToken cancellationToken)
    {
        CloseCount++;
        return ValueTask.CompletedTask;
    }
}

internal static class DistanceFixtures
{
    public static DetectionProfile Profile { get; } = DetectionProfile.CreateDefault();

    public static CalibrationInfo Calibration(
        double referencePixelWidth = 100,
        double referenceDistance = 100,
        string profileId = "default-otsu-bright-v1",
        PixelMeasureAxis axis = PixelMeasureAxis.Horizontal) => new(
            20,
            referencePixelWidth,
            referenceDistance,
            DistanceUnit.Mm,
            axis,
            "camera-1",
            new ImageSize(100, 80),
            profileId,
            new DistanceRange(20, 400));

    public static IImageLease Frame(
        ImageFrameFactory? factory = null,
        string sourceId = "camera-1",
        ImageSize? size = null,
        long index = 1)
    {
        var actualSize = size ?? new ImageSize(100, 80);
        return (factory ?? new ImageFrameFactory()).Rent(
            actualSize.Width,
            actualSize.Height,
            PixelFormat.Gray8,
            new FrameInfo(index, DateTime.UtcNow, sourceId, actualSize));
    }

    public static TargetCandidate Candidate(RoiRect roi, double pixelSize = 100, double confidence = 0.9) => new(
        new RoiRect(roi.X + 2, roi.Y + 2, Math.Max(2, roi.Width - 4), Math.Max(2, roi.Height - 4)),
        new Point2D(roi.X + roi.Width / 2d, roi.Y + roi.Height / 2d),
        pixelSize,
        confidence);

    public static VisionEngineInfo EngineInfo(VisionCapability capabilities) => new(
        "test-engine",
        "Test Engine",
        new Version(1, 0),
        true,
        VisionLicenseState.NotRequired,
        capabilities);
}
