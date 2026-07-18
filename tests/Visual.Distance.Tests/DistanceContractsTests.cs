using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Tests;

public sealed class DistanceContractsTests
{
    [Fact]
    public void Distance_range_and_calibration_should_validate_inputs()
    {
        Assert.Throws<VisionException>(() => new DistanceRange(0, 10));
        Assert.Throws<VisionException>(() => new DistanceRange(10, 5));
        Assert.Throws<VisionException>(() => new CalibrationInfo(
            20,
            100,
            500,
            DistanceUnit.Mm,
            PixelMeasureAxis.Horizontal,
            "camera-1",
            new ImageSize(100, 80),
            DistanceFixtures.Profile.ProfileId,
            new DistanceRange(20, 400)));
    }

    [Fact]
    public void Request_should_snapshot_targets_and_reject_duplicates()
    {
        using var frame = DistanceFixtures.Frame();
        var targets = new List<TargetRegion> { new("a", new RoiRect(0, 0, 20, 20)) };
        var request = new DistanceRequest(frame.Frame, targets);
        targets.Add(new TargetRegion("b", new RoiRect(20, 0, 20, 20)));

        Assert.Single(request.Targets);
        Assert.Throws<VisionException>(() => new DistanceRequest(frame.Frame,
        [
            new TargetRegion("same", new RoiRect(0, 0, 20, 20)),
            new TargetRegion("same", new RoiRect(20, 0, 20, 20))
        ]));
    }

    [Fact]
    public void Builder_should_require_detection_capability()
    {
        var missing = Assert.Throws<VisionException>(() => new DistanceServiceBuilder().Build());
        Assert.Equal(VisionErrorCode.InvalidInput, missing.ErrorCode);

        var engine = new TestEngine(
            DistanceFixtures.EngineInfo(VisionCapability.Tracking),
            new TestDetector((_, _) => null));
        var unavailable = Assert.Throws<VisionException>(() =>
            new DistanceServiceBuilder().WithEngine(engine).Build());
        Assert.Equal(VisionErrorCode.EngineUnavailable, unavailable.ErrorCode);
    }

    [Fact]
    public async Task Explicit_detector_should_take_priority_over_engine_detector()
    {
        var explicitDetector = new TestDetector((_, roi) => DistanceFixtures.Candidate(roi));
        var engineDetector = new TestDetector((_, _) => throw new InvalidOperationException());
        var engine = new TestEngine(
            DistanceFixtures.EngineInfo(VisionCapability.Detection),
            engineDetector);
        await using var service = new DistanceServiceBuilder()
            .WithEngine(engine)
            .WithDetector(explicitDetector)
            .Build();
        service.SetCalibration(DistanceFixtures.Calibration());
        using var frame = DistanceFixtures.Frame();

        var results = await service.MeasureAsync(new DistanceRequest(
            frame.Frame,
            [new TargetRegion("target", new RoiRect(0, 0, 30, 30))]));

        Assert.Single(results);
        Assert.Equal(1, explicitDetector.CallCount);
        Assert.Equal(0, engine.DetectorCreateCount);
    }
}
