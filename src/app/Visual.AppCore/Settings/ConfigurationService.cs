using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;

namespace Visual.AppCore.Settings;

public sealed class ConfigurationService(AppSettingsService settingsService)
{
    public async ValueTask<AppSettings> RestoreAsync(IDistanceService service, CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync(cancellationToken);
        if (settings.DistanceConfiguration is { } configuration)
        {
            service.Configure(configuration);
        }

        return settings;
    }

    public ValueTask PersistAsync(
        IDistanceService service,
        string engineId,
        string? videoPath,
        RoiRect? roi,
        CancellationToken cancellationToken = default) =>
        settingsService.SaveAsync(
            new AppSettings(engineId, videoPath, roi, service.ExportConfig()),
            cancellationToken);
}
