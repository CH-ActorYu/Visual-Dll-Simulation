using Visual.Abstractions.Contracts;
using Visual.AppCore.Settings;

namespace Visual.AppCore.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsRoiAndModuleConfiguration()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visual-appcore-{Guid.NewGuid():N}");
        var path = System.IO.Path.Combine(directory, "settings.json");
        try
        {
            var service = new AppSettingsService(path);
            var expected = new AppSettings(
                "OpenCv",
                "sample.mp4",
                new RoiRect(10, 20, 100, 80),
                new ModuleConfiguration(1, "OpenCv", "{\"calibration\":null}"));

            await service.SaveAsync(expected);
            var actual = await service.LoadAsync();

            Assert.Equal(expected, actual);
        }
        finally
        {
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "settings.json");
        var service = new AppSettingsService(path);

        var result = await service.LoadAsync();

        Assert.Equal(AppSettings.Default, result);
    }
}
