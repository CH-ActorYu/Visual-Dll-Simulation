using Visual.Abstractions.Contracts;
using Visual.AppCore.Runtime;
using Visual.Vision.Contracts;

namespace Visual.AppCore.Tests;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void GetEngines_DeduplicatesEngineIdsAcrossProviders()
    {
        var first = new StubProvider(new VisionEngineInfo(
            "OpenCv", "OpenCV", new Version(1, 0), true,
            VisionLicenseState.NotRequired, VisionCapability.Detection));
        var second = new StubProvider(new VisionEngineInfo(
            "opencv", "Duplicate", new Version(2, 0), true,
            VisionLicenseState.NotRequired, VisionCapability.Detection));

        var result = new ModuleCatalog([first, second]).GetEngines();

        Assert.Single(result);
        Assert.Equal("OpenCv", result[0].Id);
    }

    private sealed class StubProvider(VisionEngineInfo info) : IVisionEngineProvider
    {
        public IReadOnlyList<VisionEngineInfo> GetAvailableEngines() => [info];

        public IVisionEngine Get(string id) => throw new NotSupportedException();
    }
}
