using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvEngine : IVisionEngine
{
    public const string EngineId = "OpenCv";

    public OpenCvEngine()
    {
        Info = CreateInfo();
    }

    public VisionEngineInfo Info { get; }

    public ITargetDetector CreateDetector(DetectionProfile profile)
    {
        EnsureAvailable();
        return new OpenCvBlobDetector(profile);
    }

    public ITracker CreateTracker(DetectionProfile profile)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(profile);
        return new OpenCvTracker(profile.RedetectInterval);
    }

    public ITargetModelFactory CreateTargetModelFactory()
    {
        EnsureAvailable();
        return new OpenCvTargetModelFactory();
    }

    public IPreprocessOperator CreatePreprocessor(DetectionProfile profile)
    {
        EnsureAvailable();
        return new OpenCvDefaultPreprocessOperator(profile);
    }

    private void EnsureAvailable()
    {
        if (!Info.IsAvailable)
        {
            throw new VisionException(VisionErrorCode.EngineUnavailable, "OpenCV native runtime is unavailable.");
        }
    }

    private static VisionEngineInfo CreateInfo()
    {
        try
        {
            var versionText = Cv2.GetVersionString() ?? "0.0";
            var version = Version.TryParse(versionText.Split(' ')[0], out var parsed)
                ? parsed
                : new Version(0, 0);
            return new VisionEngineInfo(
                EngineId,
                "OpenCV",
                version,
                true,
                VisionLicenseState.NotRequired,
                VisionCapability.Preprocess | VisionCapability.Detection |
                VisionCapability.Tracking | VisionCapability.Camera |
                VisionCapability.TargetRegistration);
        }
        catch
        {
            return new VisionEngineInfo(
                EngineId,
                "OpenCV",
                new Version(0, 0),
                false,
                VisionLicenseState.NotRequired,
                VisionCapability.None);
        }
    }
}

public sealed class OpenCvEngineProvider : IVisionEngineProvider
{
    private readonly OpenCvEngine _engine = new();

    public IReadOnlyList<VisionEngineInfo> GetAvailableEngines() => [_engine.Info];

    public IVisionEngine Get(string id)
    {
        if (!string.Equals(id, OpenCvEngine.EngineId, StringComparison.OrdinalIgnoreCase))
        {
            throw new VisionException(VisionErrorCode.EngineUnavailable, $"Vision engine '{id}' is not registered.");
        }

        if (!_engine.Info.IsAvailable)
        {
            throw new VisionException(VisionErrorCode.EngineUnavailable, "OpenCV native runtime is unavailable.");
        }

        return _engine;
    }
}
