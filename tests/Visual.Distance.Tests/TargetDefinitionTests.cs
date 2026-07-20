using System.Text.Json;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Distance.Tests;

public sealed class TargetDefinitionTests
{
    [Fact]
    public void Definition_should_capture_model_and_measurement_metadata_without_image_data()
    {
        using var model = new TestTargetModel(PixelMeasureAxis.MajorAxis);

        var definition = TargetDefinition.FromModel(
            "part-a",
            "targets/part-a.png",
            "0123456789ABCDEF",
            42.5,
            DistanceUnit.Mm,
            model);
        var json = JsonSerializer.Serialize(definition);
        var restored = JsonSerializer.Deserialize<TargetDefinition>(json);

        Assert.Equal(definition, restored);
        Assert.Equal(PixelMeasureAxis.MajorAxis, definition.MeasureAxis);
        Assert.Equal("TestEngine", definition.EngineId);
        Assert.Equal("test-model-v1", definition.ModelVersion);
        Assert.DoesNotContain("ReferencePreview", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ImageFrame", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Definition_should_reject_invalid_size_axis_and_schema()
    {
        Assert.Throws<VisionException>(() => Create(physicalSize: 0));
        Assert.Throws<VisionException>(() => Create(measureAxis: (PixelMeasureAxis)999));
        Assert.Throws<VisionException>(() => Create(schemaVersion: 2));
    }

    private static TargetDefinition Create(
        double physicalSize = 10,
        PixelMeasureAxis measureAxis = PixelMeasureAxis.Horizontal,
        int schemaVersion = TargetDefinition.CurrentSchemaVersion) => new(
        "target",
        "resource",
        "hash",
        physicalSize,
        DistanceUnit.Mm,
        measureAxis,
        "engine",
        "model-v1",
        "profile",
        schemaVersion);

    private sealed class TestTargetModel : ITargetModel
    {
        private readonly IImageLease _preview;

        public TestTargetModel(PixelMeasureAxis axis)
        {
            ReferenceShape = new TargetShape(
                [new Point2D(0, 0), new Point2D(10, 0), new Point2D(5, 10)],
                new RoiRect(0, 0, 10, 10),
                new Point2D(5, 4),
                axis,
                10);
            _preview = new ImageFrameFactory().Rent(10, 10, PixelFormat.Bgra32);
        }

        public string EngineId => "TestEngine";

        public string ModelVersion => "test-model-v1";

        public string DetectionProfileId => "test-profile";

        public TargetShape ReferenceShape { get; }

        public IImageLease AcquireReferencePreview() => _preview.Retain();

        public void Dispose() => _preview.Dispose();
    }
}
