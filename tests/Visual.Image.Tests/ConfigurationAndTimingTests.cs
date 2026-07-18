using Visual.Abstractions.Contracts;

namespace Visual.Image.Tests;

public sealed class ConfigurationAndTimingTests
{
    [Fact]
    public void Frame_timing_should_calculate_processing_and_end_to_end_duration()
    {
        var captured = new DateTime(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
        var timing = new FrameTiming(
            captured,
            captured.AddMilliseconds(5),
            captured.AddMilliseconds(17));

        Assert.Equal(TimeSpan.FromMilliseconds(12), timing.ProcessingDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(17), timing.EndToEndDuration);
    }

    [Fact]
    public void Frame_timing_should_reject_reversed_timestamps()
    {
        var now = DateTime.UtcNow;

        var exception = Assert.Throws<VisionException>(() => new FrameTiming(now, now.AddMilliseconds(2), now.AddMilliseconds(1)));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Module_configuration_should_validate_and_preserve_versioned_json()
    {
        const string json = "{\"threshold\":128}";

        var configuration = new ModuleConfiguration(2, "OpenCv", json);

        Assert.Equal(2, configuration.SchemaVersion);
        Assert.Equal("OpenCv", configuration.EngineId);
        Assert.Equal(json, configuration.Json);
    }

    [Fact]
    public void Module_configuration_should_wrap_invalid_json_as_invalid_input()
    {
        var exception = Assert.Throws<VisionException>(() => new ModuleConfiguration(1, "OpenCv", "{"));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void Vision_result_should_reject_error_code_on_valid_result()
    {
        var now = DateTime.UtcNow;
        var timing = new FrameTiming(now, now, now);

        var exception = Assert.Throws<VisionException>(() =>
            new TestResult(VisionResultStatus.Valid, VisionErrorCode.ModuleSpecific, 1, timing, 0, null));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Coordinate_system_should_define_documented_image_conventions()
    {
        Assert.Contains(CoordinateSystem.OriginTopLeft, Enum.GetValues<CoordinateSystem>());
        Assert.Contains(CoordinateSystem.AxisRightDown, Enum.GetValues<CoordinateSystem>());
        Assert.Contains(CoordinateSystem.Pixel, Enum.GetValues<CoordinateSystem>());
    }

    private sealed record TestResult : VisionResultBase
    {
        public TestResult(
            VisionResultStatus status,
            VisionErrorCode? errorCode,
            double confidence,
            FrameTiming timing,
            long frameIndex,
            string? sourceId)
            : base(status, errorCode, confidence, timing, frameIndex, sourceId)
        {
        }
    }
}
