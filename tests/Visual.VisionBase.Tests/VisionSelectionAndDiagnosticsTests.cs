using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using Visual.VisionBase.Contracts;

namespace Visual.VisionBase.Tests;

public sealed class VisionSelectionAndDiagnosticsTests
{
    [Fact]
    public void Result_factory_should_preserve_frame_candidate_and_status()
    {
        var now = DateTime.UtcNow;
        var info = new FrameInfo(42, now, "camera-1", new ImageSize(10, 10));
        var timing = new FrameTiming(now, now.AddMilliseconds(1), now.AddMilliseconds(3));
        var candidate = new TargetCandidate(new RoiRect(1, 1, 3, 3), new Point2D(2, 2), 3, 0.8);

        var success = MeasurementResultFactory.CreateSuccess(info, timing, candidate);
        var missing = MeasurementResultFactory.CreateNotDetected(info, timing);
        var failed = MeasurementResultFactory.CreateFailed(info, timing, VisionErrorCode.DecodingFailed);

        Assert.Equal(VisionResultStatus.Valid, success.Status);
        Assert.Same(candidate, success.Candidate);
        Assert.Equal(42, success.FrameIndex);
        Assert.Equal(VisionResultStatus.NotDetected, missing.Status);
        Assert.Equal(VisionResultStatus.Failed, failed.Status);
        Assert.Equal(VisionErrorCode.DecodingFailed, failed.ErrorCode);
    }

    [Fact]
    public void Engine_selector_should_honor_preference_and_validate_license()
    {
        var fallback = new TestEngine("fallback", true, VisionLicenseState.NotRequired, VisionCapability.Detection);
        var preferred = new TestEngine("preferred", true, VisionLicenseState.Valid, VisionCapability.Detection | VisionCapability.Tracking);

        Assert.Same(preferred, VisionEngineSelector.Select(
            [fallback, preferred],
            VisionCapability.Detection,
            "PREFERRED"));
        Assert.Same(fallback, VisionEngineSelector.Select([fallback], VisionCapability.Detection));

        var unlicensed = new TestEngine("licensed", true, VisionLicenseState.Invalid, VisionCapability.Detection);
        var exception = Assert.Throws<VisionException>(() =>
            VisionEngineSelector.CheckCapability(unlicensed, VisionCapability.Detection));
        Assert.Equal(VisionErrorCode.LicenseInvalid, exception.ErrorCode);
    }

    [Fact]
    public void Diagnostics_should_be_bounded_and_isolate_sink_failures()
    {
        var collecting = new CollectingSink();
        var diagnostics = new VisionDiagnostics([collecting, new ThrowingSink()], capacity: 2);

        diagnostics.RuntimeStarted("source", "engine");
        diagnostics.FrameProcessed("source", 1, "engine", TimeSpan.FromMilliseconds(2), 0);
        diagnostics.RuntimeStopped("source", "engine");

        var snapshot = diagnostics.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(VisionDiagnosticKind.FrameProcessed, snapshot[0].Kind);
        Assert.Equal(VisionDiagnosticKind.RuntimeStopped, snapshot[1].Kind);
        Assert.Equal(3, collecting.Records.Count);
        Assert.Equal(3, diagnostics.SinkFailureCount);
    }

    private sealed class CollectingSink : IVisionDiagnosticSink
    {
        public List<VisionDiagnosticRecord> Records { get; } = [];

        public void Write(VisionDiagnosticRecord diagnostic) => Records.Add(diagnostic);
    }

    private sealed class ThrowingSink : IVisionDiagnosticSink
    {
        public void Write(VisionDiagnosticRecord diagnostic) => throw new InvalidOperationException("sink failure");
    }

    private sealed class TestEngine(
        string id,
        bool available,
        VisionLicenseState license,
        VisionCapability capabilities) : IVisionEngine
    {
        public VisionEngineInfo Info { get; } = new(id, id, new Version(1, 0), available, license, capabilities);

        public ITargetDetector CreateDetector(DetectionProfile profile) => throw new NotSupportedException();

        public ITracker CreateTracker(DetectionProfile profile) => throw new NotSupportedException();

        public ITargetModelFactory CreateTargetModelFactory() =>
            throw new NotSupportedException();

        public ITargetModelTracker CreateTargetModelTracker(DetectionProfile profile) =>
            throw new NotSupportedException();

        public IPreprocessOperator CreatePreprocessor(DetectionProfile profile) => throw new NotSupportedException();
    }
}
