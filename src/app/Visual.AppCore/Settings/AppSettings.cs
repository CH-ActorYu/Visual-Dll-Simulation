using Visual.Abstractions.Contracts;

namespace Visual.AppCore.Settings;

public sealed record AppSettings(
    string EngineId,
    string? VideoPath,
    RoiRect? TargetRoi,
    ModuleConfiguration? DistanceConfiguration)
{
    public static AppSettings Default { get; } = new("OpenCv", null, null, null);
}
