using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Contracts;

public static class VisionEngineSelector
{
    public static IVisionEngine Select(
        IEnumerable<IVisionEngine> engines,
        VisionCapability requiredCapabilities,
        string? preferredEngineId = null)
    {
        if (engines is null || requiredCapabilities == VisionCapability.None)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Engines and required capabilities are required.");
        }

        var candidates = engines.ToArray();
        if (candidates.Any(engine => engine is null))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Engine collection cannot contain null entries.");
        }

        if (!string.IsNullOrWhiteSpace(preferredEngineId))
        {
            var preferred = candidates.FirstOrDefault(
                engine => string.Equals(engine.Info.Id, preferredEngineId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                CheckCapability(preferred, requiredCapabilities);
                return preferred;
            }
        }

        foreach (var engine in candidates)
        {
            if (IsUsable(engine.Info, requiredCapabilities))
            {
                return engine;
            }
        }

        throw new VisionException(VisionErrorCode.EngineUnavailable, "No available vision engine provides the required capabilities.");
    }

    public static void CheckCapability(IVisionEngine engine, VisionCapability requiredCapabilities)
    {
        if (engine is null || requiredCapabilities == VisionCapability.None)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "An engine and required capabilities are required.");
        }

        var info = engine.Info;
        if (!info.IsAvailable)
        {
            throw new VisionException(VisionErrorCode.EngineUnavailable, $"Vision engine {info.Id} is unavailable.");
        }

        if (info.LicenseState is VisionLicenseState.Invalid or VisionLicenseState.Unavailable)
        {
            throw new VisionException(VisionErrorCode.LicenseInvalid, $"Vision engine {info.Id} does not have a valid license.");
        }

        if (!info.Supports(requiredCapabilities))
        {
            throw new VisionException(VisionErrorCode.EngineUnavailable, $"Vision engine {info.Id} lacks the required capabilities.");
        }
    }

    private static bool IsUsable(VisionEngineInfo info, VisionCapability requiredCapabilities) =>
        info.IsAvailable &&
        info.LicenseState is not VisionLicenseState.Invalid and not VisionLicenseState.Unavailable &&
        info.Supports(requiredCapabilities);
}
