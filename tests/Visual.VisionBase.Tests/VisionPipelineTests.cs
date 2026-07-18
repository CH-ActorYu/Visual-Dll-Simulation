using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using Visual.VisionBase.Contracts;

namespace Visual.VisionBase.Tests;

public sealed class VisionPipelineTests
{
    [Fact]
    public void Roi_and_geometry_helpers_should_clamp_validate_and_convert()
    {
        var validator = new RoiValidator();
        var clamped = validator.Validate(new RoiRect(-2, 3, 8, 9), new ImageSize(10, 10));

        Assert.Equal(new RoiRect(0, 3, 6, 7), clamped);
        Assert.Equal(new Point2D(3, 6.5), GeometryUtils.CenterOf(clamped));
        Assert.Equal(12.5, GeometryUtils.PixelToPhysical(25, 10, 20));

        var tooSmall = Assert.Throws<VisionException>(() =>
            validator.Validate(new RoiRect(9, 9, 5, 5), new ImageSize(10, 10)));
        Assert.Equal(VisionErrorCode.InvalidInput, tooSmall.ErrorCode);
    }

    [Fact]
    public void Preprocess_pipeline_should_release_intermediate_leases()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        using var source = factory.Rent(4, 4, PixelFormat.Gray8);
        var pipeline = new PreprocessPipeline()
            .Add(new CloneOperator(factory, 10))
            .Add(new CloneOperator(factory, 20));

        using var result = pipeline.Run(source.Frame);

        Assert.Equal(20, result.Frame.Data.Span[0]);
        Assert.Equal(2, pool.GetStatistics().Outstanding);
        result.Dispose();
        source.Dispose();
        Assert.Equal(0, pool.GetStatistics().Outstanding);
    }

    [Fact]
    public void Preprocess_pipeline_should_release_intermediate_on_failure()
    {
        var pool = new ImageMemoryPool();
        var factory = new ImageFrameFactory(pool);
        using var source = factory.Rent(4, 4, PixelFormat.Gray8);
        var pipeline = new PreprocessPipeline()
            .Add(new CloneOperator(factory, 1))
            .Add(new ThrowingOperator());

        var exception = Assert.Throws<VisionException>(() => pipeline.Run(source.Frame));

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
        Assert.Equal(1, pool.GetStatistics().Outstanding);
    }

    [Fact]
    public void Detection_pipeline_should_use_tracker_then_fall_back_to_detector()
    {
        using var lease = CreateFrame();
        var roi = new RoiRect(0, 0, 20, 20);
        var tracked = Candidate(2, 2);
        var detected = Candidate(8, 8);
        var detector = new TestDetector(detected);
        var tracker = new TestTracker(tracked);
        var pipeline = new DetectionPipeline();

        var first = pipeline.Run(lease.Frame, [roi], detector, tracker);
        Assert.Same(tracked, first[0]);
        Assert.Equal(0, detector.CallCount);

        tracker.Next = Candidate(30, 30);
        var second = pipeline.Run(lease.Frame, [roi], detector, tracker);
        Assert.Same(detected, second[0]);
        Assert.Equal(1, detector.CallCount);
        Assert.Equal(detected.BoundingBox, tracker.InitializedRegion);
    }

    [Fact]
    public void Detection_pipeline_should_align_multiple_rois_and_reject_invalid_detector_output()
    {
        using var lease = CreateFrame();
        var regions = new[] { new RoiRect(0, 0, 20, 20), new RoiRect(20, 0, 20, 20) };
        var trackers = new ITracker?[] { new TestTracker(Candidate(2, 2)), null };
        var detector = new TestDetector(Candidate(22, 2));
        var pipeline = new DetectionPipeline();

        var results = pipeline.Run(lease.Frame, regions, detector, trackers);
        Assert.Equal(2, results.Count);
        Assert.NotNull(results[0]);
        Assert.NotNull(results[1]);

        var invalid = new TestDetector(Candidate(45, 45));
        var exception = Assert.Throws<VisionException>(() =>
            pipeline.Run(lease.Frame, [regions[0]], invalid, (ITracker?)null));
        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
    }

    [Fact]
    public void Tracker_initialization_failures_should_be_normalized()
    {
        using var lease = CreateFrame();
        var tracker = new TestTracker(null) { ThrowOnInitialize = true };
        var pipeline = new DetectionPipeline();

        var exception = Assert.Throws<VisionException>(() => pipeline.Run(
            lease.Frame,
            [new RoiRect(0, 0, 20, 20)],
            new TestDetector(Candidate(2, 2)),
            tracker));

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
    }

    private static IImageLease CreateFrame() => new ImageFrameFactory().Rent(
        50,
        50,
        PixelFormat.Gray8,
        new FrameInfo(1, DateTime.UtcNow, "test", new ImageSize(50, 50)));

    private static TargetCandidate Candidate(int x, int y) => new(
        new RoiRect(x, y, 4, 4),
        new Point2D(x + 2, y + 2),
        4,
        0.9);

    private sealed class CloneOperator(ImageFrameFactory factory, byte value) : IPreprocessOperator
    {
        public IImageLease Apply(ImageFrame source) => factory.Rent(
            source.Info.Size.Width,
            source.Info.Size.Height,
            source.Format,
            source.Info,
            memory => memory.Span.Fill(value),
            source.Stride);
    }

    private sealed class ThrowingOperator : IPreprocessOperator
    {
        public IImageLease Apply(ImageFrame source) => throw new InvalidOperationException("native failure");
    }

    private sealed class TestDetector(TargetCandidate? next) : ITargetDetector
    {
        public int CallCount { get; private set; }

        public TargetCandidate? Detect(ImageFrame frame, RoiRect roi)
        {
            CallCount++;
            return next;
        }
    }

    private sealed class TestTracker(TargetCandidate? next) : ITracker
    {
        public TargetCandidate? Next { get; set; } = next;

        public RoiRect? InitializedRegion { get; private set; }

        public bool ThrowOnInitialize { get; set; }

        public void Initialize(ImageFrame frame, RoiRect roi)
        {
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("native failure");
            }

            InitializedRegion = roi;
        }

        public TargetCandidate? Update(ImageFrame frame) => Next;
    }
}
