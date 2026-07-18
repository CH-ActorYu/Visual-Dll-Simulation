using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Vision.Contracts;

namespace Visual.AppCore.Runtime;

public sealed class ModuleCatalog
{
    private readonly IReadOnlyList<IVisionEngineProvider> _providers;

    public ModuleCatalog(IEnumerable<IVisionEngineProvider> providers) =>
        _providers = providers?.ToArray() ?? throw new ArgumentNullException(nameof(providers));

    public IReadOnlyList<VisionEngineInfo> GetEngines() => _providers
        .SelectMany(provider => provider.GetAvailableEngines())
        .GroupBy(info => info.Id, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IDistanceService CreateDistanceService(string engineId, DetectionProfile? profile = null)
    {
        foreach (var provider in _providers)
        {
            if (provider.GetAvailableEngines().Any(info =>
                    string.Equals(info.Id, engineId, StringComparison.OrdinalIgnoreCase)))
            {
                return new DistanceServiceBuilder()
                    .WithEngine(provider.Get(engineId))
                    .WithDetectionProfile(profile ?? DetectionProfile.CreateDefault())
                    .Build();
            }
        }

        throw new VisionException(VisionErrorCode.EngineUnavailable, $"Vision engine '{engineId}' is not registered.");
    }
}
