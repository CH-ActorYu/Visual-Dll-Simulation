using Visual.Abstractions.Contracts;
using Visual.Distance.Internal;
using Visual.Vision.Contracts;
using Visual.VisionBase.Contracts;

namespace Visual.Distance.Contracts;

public sealed class DistanceServiceBuilder
{
    private DetectionProfile _profile = DetectionProfile.CreateDefault();
    private IVisionEngine? _engine;
    private ITargetDetector? _detector;

    public DistanceServiceBuilder WithEngine(IVisionEngine engine)
    {
        _engine = engine ?? throw Invalid("A vision engine is required.");
        return this;
    }

    public DistanceServiceBuilder WithDetector(ITargetDetector detector)
    {
        _detector = detector ?? throw Invalid("A target detector is required.");
        return this;
    }

    public DistanceServiceBuilder WithDetectionProfile(DetectionProfile profile)
    {
        _profile = profile ?? throw Invalid("A detection profile is required.");
        return this;
    }

    public IDistanceService Build()
    {
        ITargetDetector detector;
        if (_detector is not null)
        {
            detector = _detector;
        }
        else
        {
            var engine = _engine ?? throw Invalid("A detector or vision engine is required.");
            VisionEngineSelector.CheckCapability(engine, VisionCapability.Detection);
            detector = engine.CreateDetector(_profile);
        }

        Func<ITracker>? trackerFactory = null;
        if (_engine is { } trackingEngine &&
            trackingEngine.Info.IsAvailable &&
            trackingEngine.Info.Supports(VisionCapability.Tracking) &&
            trackingEngine.Info.LicenseState is not VisionLicenseState.Invalid and not VisionLicenseState.Unavailable)
        {
            trackerFactory = () => trackingEngine.CreateTracker(_profile);
        }

        return new DistanceServiceImpl(
            detector,
            trackerFactory,
            _profile,
            _engine?.Info.Id ?? "CustomDetector");
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
