using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Tests;

public sealed class DistanceServiceTests
{
    [Theory]
    [InlineData(100, 100)]
    [InlineData(50, 200)]
    [InlineData(200, 50)]
    public async Task Measurement_should_follow_monocular_calibration_formula(
        double pixelSize,
        double expectedDistance)
    {
        var detector = new TestDetector((_, roi) => DistanceFixtures.Candidate(roi, pixelSize));
        await using var service = new DistanceServiceBuilder().WithDetector(detector).Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();

        var result = Assert.Single(await service.MeasureAsync(new DistanceRequest(
            frame.Frame,
            [new TargetRegion("target", new RoiRect(5, 5, 40, 40))])));

        Assert.Equal(VisionResultStatus.Valid, result.Status);
        Assert.Equal(expectedDistance, result.Distance!.Value, 6);
        Assert.Equal(DistanceUnit.Mm, result.Unit);
        Assert.Equal(pixelSize, result.TargetPixelSize);
        Assert.False(frame.IsReleased);
    }

    [Fact]
    public async Task Multiple_targets_should_preserve_count_order_and_ids()
    {
        var detector = new TestDetector((_, roi) => DistanceFixtures.Candidate(roi));
        await using var service = new DistanceServiceBuilder().WithDetector(detector).Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();
        var targets = new[]
        {
            new TargetRegion("left", new RoiRect(0, 0, 30, 30)),
            new TargetRegion("right", new RoiRect(40, 0, 30, 30))
        };

        var results = await service.MeasureAsync(new DistanceRequest(frame.Frame, targets));

        Assert.Equal(2, results.Count);
        Assert.Equal("left", results[0].TargetId);
        Assert.Equal("right", results[1].TargetId);
    }

    [Fact]
    public async Task Uncalibrated_single_frame_should_throw_custom_exception()
    {
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, _) => null))
            .Build();
        using var frame = DistanceFixtures.Frame();

        await Assert.ThrowsAsync<NotCalibratedException>(async () => await service.MeasureAsync(
            new DistanceRequest(frame.Frame, [new TargetRegion("target", new RoiRect(0, 0, 20, 20))])));
        Assert.False(frame.IsReleased);
    }

    [Fact]
    public async Task Missing_target_should_return_not_detected_without_distance()
    {
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, _) => null))
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();

        var result = Assert.Single(await service.MeasureAsync(new DistanceRequest(
            frame.Frame,
            [new TargetRegion("target", new RoiRect(0, 0, 20, 20))])));

        Assert.Equal(VisionResultStatus.NotDetected, result.Status);
        Assert.Null(result.Distance);
        Assert.Null(result.TargetCenter);
    }

    [Fact]
    public async Task Detector_failure_should_be_normalized_for_single_frame_calls()
    {
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, _) => throw new InvalidOperationException("native failure")))
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();

        var exception = await Assert.ThrowsAsync<VisionException>(async () => await service.MeasureAsync(
            new DistanceRequest(frame.Frame, [new TargetRegion("target", new RoiRect(0, 0, 20, 20))])));

        Assert.Equal(VisionErrorCode.ModuleSpecific, exception.ErrorCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task Distance_outside_validated_range_should_return_failed()
    {
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, roi) => DistanceFixtures.Candidate(roi, pixelSize: 10)))
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();

        var result = Assert.Single(await service.MeasureAsync(new DistanceRequest(
            frame.Frame,
            [new TargetRegion("target", new RoiRect(0, 0, 20, 20))])));

        Assert.Equal(VisionResultStatus.Failed, result.Status);
        Assert.Equal(VisionErrorCode.ModuleSpecific, result.ErrorCode);
        Assert.Null(result.Distance);
    }

    [Fact]
    public async Task Camera_or_resolution_change_should_invalidate_calibration()
    {
        await using var service = new DistanceServiceBuilder()
            .WithDetector(new TestDetector((_, roi) => DistanceFixtures.Candidate(roi)))
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame(sourceId: "camera-2");

        await Assert.ThrowsAsync<NotCalibratedException>(async () => await service.MeasureAsync(
            new DistanceRequest(frame.Frame, [new TargetRegion("target", new RoiRect(0, 0, 20, 20))])));
        Assert.False(service.IsCalibrated);
    }

    [Fact]
    public async Task Rejected_calibration_should_preserve_previous_valid_snapshot()
    {
        var detector = new TestDetector((_, roi) => DistanceFixtures.Candidate(roi));
        await using var service = new DistanceServiceBuilder().WithDetector(detector).Build();
        service.SetCalibration(DistanceFixtures.Calibration());

        Assert.Throws<VisionException>(() => service.SetCalibration(
            DistanceFixtures.Calibration(profileId: "different")));
        Assert.True(service.IsCalibrated);
        using var frame = DistanceFixtures.Frame();
        var result = Assert.Single(await service.MeasureAsync(new DistanceRequest(
            frame.Frame,
            [new TargetRegion("target", new RoiRect(0, 0, 20, 20))])));
        Assert.Equal(VisionResultStatus.Valid, result.Status);
    }

    [Fact]
    public async Task Configuration_should_round_trip_and_reject_mismatch_atomically()
    {
        var detector = new TestDetector((_, roi) => DistanceFixtures.Candidate(roi));
        await using var source = new DistanceServiceBuilder().WithDetector(detector).Build();
        source.SetCalibration(DistanceFixtures.Calibration());
        var exported = source.ExportConfig();
        await using var restored = new DistanceServiceBuilder().WithDetector(detector).Build();

        restored.Configure(exported);
        Assert.True(restored.IsCalibrated);
        var invalid = new ModuleConfiguration(exported.SchemaVersion, "wrong-engine", exported.Json);
        Assert.Throws<VisionException>(() => restored.Configure(invalid));
        Assert.True(restored.IsCalibrated);
    }

    [Fact]
    public async Task Service_should_dispose_owned_trackers()
    {
        var trackers = new List<TestTracker>();
        var engine = new TestEngine(
            DistanceFixtures.EngineInfo(VisionCapability.Detection | VisionCapability.Tracking),
            new TestDetector((_, roi) => DistanceFixtures.Candidate(roi)),
            () =>
            {
                var tracker = new TestTracker();
                trackers.Add(tracker);
                return tracker;
            });
        var service = new DistanceServiceBuilder().WithEngine(engine).Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();
        await service.MeasureAsync(new DistanceRequest(
            frame.Frame,
            [new TargetRegion("target", new RoiRect(0, 0, 20, 20))]));

        await service.DisposeAsync();

        var tracker = Assert.Single(trackers);
        Assert.Equal(1, tracker.InitializeCount);
        Assert.Equal(1, tracker.DisposeCount);
    }
}
