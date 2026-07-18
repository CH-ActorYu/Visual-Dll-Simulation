using System.Reflection;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Tests;

public sealed class OpenCvEngineTests
{
    [Fact]
    public void Engine_should_report_runtime_and_create_p0_capabilities()
    {
        var engine = new OpenCvEngine();

        Assert.True(engine.Info.IsAvailable);
        Assert.True(engine.Info.Supports(
            VisionCapability.Preprocess | VisionCapability.Detection |
            VisionCapability.Tracking | VisionCapability.Camera));
        Assert.Equal(VisionLicenseState.NotRequired, engine.Info.LicenseState);
        Assert.IsType<OpenCvBlobDetector>(engine.CreateDetector(DetectionProfile.CreateDefault()));
        Assert.IsType<OpenCvTracker>(engine.CreateTracker(DetectionProfile.CreateDefault()));
        Assert.IsType<OpenCvDefaultPreprocessOperator>(engine.CreatePreprocessor(DetectionProfile.CreateDefault()));
    }

    [Fact]
    public void Provider_should_be_case_insensitive_and_reject_unknown_engine()
    {
        var provider = new OpenCvEngineProvider();

        Assert.Single(provider.GetAvailableEngines());
        Assert.IsType<OpenCvEngine>(provider.Get("opencv"));
        var exception = Assert.Throws<VisionException>(() => provider.Get("missing"));
        Assert.Equal(VisionErrorCode.EngineUnavailable, exception.ErrorCode);
    }

    [Fact]
    public void Public_engine_api_should_not_expose_opencv_native_types()
    {
        var violations = typeof(OpenCvEngine).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(member => member switch
            {
                MethodInfo method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType),
                ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
                PropertyInfo property => [property.PropertyType],
                FieldInfo field => [field.FieldType],
                _ => []
            })
            .Where(type => type.Namespace?.StartsWith("OpenCvSharp", StringComparison.Ordinal) == true)
            .Select(type => type.FullName)
            .Distinct()
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Grayscale_and_threshold_should_return_owned_expected_images()
    {
        var factory = new ImageFrameFactory();
        using var color = factory.Rent(
            2,
            1,
            PixelFormat.Bgr24,
            initialize: memory =>
            {
                memory.Span[0] = 255;
                memory.Span[1] = 255;
                memory.Span[2] = 255;
                memory.Span[3] = 0;
                memory.Span[4] = 0;
                memory.Span[5] = 0;
            });

        using var gray = new OpenCvGrayscaleOperator().Apply(color.Frame);
        using var binary = new OpenCvThresholdOperator(
            ThresholdMode.Manual,
            127,
            ForegroundPolarity.Bright).Apply(gray.Frame);

        Assert.Equal(PixelFormat.Gray8, gray.Frame.Format);
        Assert.Equal(255, binary.Frame.Data.Span[0]);
        Assert.Equal(0, binary.Frame.Data.Span[1]);
        Assert.False(color.IsReleased);
    }

    [Fact]
    public void Threshold_should_honor_dark_foreground_polarity()
    {
        using var gray = new ImageFrameFactory().Rent(
            2,
            1,
            PixelFormat.Gray8,
            initialize: memory =>
            {
                memory.Span[0] = 10;
                memory.Span[1] = 240;
            });

        using var binary = new OpenCvThresholdOperator(
            ThresholdMode.Manual,
            127,
            ForegroundPolarity.Dark).Apply(gray.Frame);

        Assert.Equal(255, binary.Frame.Data.Span[0]);
        Assert.Equal(0, binary.Frame.Data.Span[1]);
    }

    [Fact]
    public void Default_preprocessor_should_release_intermediates_and_return_binary_lease()
    {
        using var source = CreateScene(40, 30, [new RoiRect(8, 6, 15, 12)]);
        var preprocess = new OpenCvDefaultPreprocessOperator(DetectionProfile.CreateDefault());

        using var output = preprocess.Apply(source.Frame);

        Assert.Equal(PixelFormat.Gray8, output.Frame.Format);
        Assert.Contains((byte)255, output.Frame.Data.ToArray());
        Assert.False(source.IsReleased);
    }

    [Fact]
    public void Blob_detector_should_select_largest_valid_candidate()
    {
        using var source = CreateScene(
            100,
            80,
            [new RoiRect(10, 10, 12, 10), new RoiRect(50, 25, 25, 20)]);
        var detector = new OpenCvBlobDetector(DetectionProfile.CreateDefault());

        var candidate = detector.Detect(source.Frame, new RoiRect(2, 2, 96, 76));

        Assert.NotNull(candidate);
        Assert.InRange(candidate.BoundingBox.X, 48, 52);
        Assert.InRange(candidate.BoundingBox.Y, 23, 27);
        Assert.InRange(candidate.PixelSize, 23, 27);
        Assert.InRange(candidate.Confidence, 0.5, 1);
    }

    [Fact]
    public void Blob_detector_should_filter_noise_and_honor_measure_axis()
    {
        using var source = CreateScene(80, 60, [new RoiRect(20, 15, 12, 25), new RoiRect(5, 5, 1, 1)]);
        var detector = new OpenCvBlobDetector(CreateProfile(PixelMeasureAxis.Vertical));

        var candidate = detector.Detect(source.Frame, new RoiRect(2, 2, 76, 56));

        Assert.NotNull(candidate);
        Assert.InRange(candidate.PixelSize, 23, 27);
    }

    [Fact]
    public void Blob_detector_should_reject_a_target_touching_roi_border()
    {
        using var source = CreateScene(50, 40, [new RoiRect(0, 8, 15, 15)]);
        var detector = new OpenCvBlobDetector(DetectionProfile.CreateDefault());

        Assert.Null(detector.Detect(source.Frame, new RoiRect(0, 0, 50, 40)));
    }

    [Fact]
    public void Tracker_should_follow_a_shifted_pattern()
    {
        using var first = CreatePatternFrame(60, 50, 15, 12);
        using var second = CreatePatternFrame(60, 50, 20, 16);
        using var tracker = new OpenCvTracker();
        tracker.Initialize(first.Frame, new RoiRect(15, 12, 10, 10));

        var candidate = tracker.Update(second.Frame);

        Assert.NotNull(candidate);
        Assert.InRange(candidate.BoundingBox.X, 19, 21);
        Assert.InRange(candidate.BoundingBox.Y, 15, 17);
        Assert.True(candidate.Confidence >= 0.5);
    }

    [Fact]
    public void Tracker_without_initialization_should_return_no_candidate()
    {
        using var frame = CreateScene(20, 20, []);
        using var tracker = new OpenCvTracker();

        Assert.Null(tracker.Update(frame.Frame));
    }

    [Fact]
    public void Tracker_should_force_redetection_at_configured_interval()
    {
        using var frame = CreatePatternFrame(60, 50, 15, 12);
        using var tracker = new OpenCvTracker(redetectInterval: 2);
        tracker.Initialize(frame.Frame, new RoiRect(15, 12, 10, 10));

        Assert.NotNull(tracker.Update(frame.Frame));
        Assert.Null(tracker.Update(frame.Frame));
    }

    private static IImageLease CreateScene(int width, int height, IReadOnlyList<RoiRect> rectangles)
    {
        var info = new FrameInfo(0, DateTime.UtcNow, "generated", new ImageSize(width, height));
        return new ImageFrameFactory().Rent(
            width,
            height,
            PixelFormat.Bgr24,
            info,
            memory =>
            {
                memory.Span.Clear();
                foreach (var rectangle in rectangles)
                {
                    FillRectangle(memory.Span, width * 3, rectangle, 255);
                }
            });
    }

    private static IImageLease CreatePatternFrame(int width, int height, int x, int y)
    {
        var info = new FrameInfo(0, DateTime.UtcNow, "tracker", new ImageSize(width, height));
        return new ImageFrameFactory().Rent(
            width,
            height,
            PixelFormat.Gray8,
            info,
            memory =>
            {
                memory.Span.Clear();
                for (var row = 0; row < 10; row++)
                {
                    for (var column = 0; column < 10; column++)
                    {
                        memory.Span[(y + row) * width + x + column] =
                            (byte)((row * 17 + column * 29) % 256);
                    }
                }
            });
    }

    private static void FillRectangle(Span<byte> data, int stride, RoiRect rectangle, byte value)
    {
        for (var y = rectangle.Y; y < rectangle.Y + rectangle.Height; y++)
        {
            for (var x = rectangle.X; x < rectangle.X + rectangle.Width; x++)
            {
                var offset = y * stride + x * 3;
                data[offset] = value;
                data[offset + 1] = value;
                data[offset + 2] = value;
            }
        }
    }

    private static DetectionProfile CreateProfile(PixelMeasureAxis axis) => new(
        "test",
        ThresholdMode.Otsu,
        128,
        ForegroundPolarity.Bright,
        5,
        3,
        0.01,
        0.9,
        0.1,
        10,
        2,
        0.5,
        axis,
        10);
}
