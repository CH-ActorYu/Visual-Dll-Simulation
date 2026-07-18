using Visual.Abstractions.Contracts;

namespace Visual.Vision.Contracts;

public sealed record VisionEngineInfo
{
    public VisionEngineInfo(
        string id,
        string name,
        Version version,
        bool isAvailable,
        VisionLicenseState licenseState,
        VisionCapability capabilities)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Vision engine ID and name are required.");
        }

        Id = id;
        Name = name;
        Version = version ?? throw new VisionException(VisionErrorCode.InvalidInput, "Vision engine version is required.");
        IsAvailable = isAvailable;
        LicenseState = licenseState;
        Capabilities = capabilities;
    }

    public string Id { get; }

    public string Name { get; }

    public Version Version { get; }

    public bool IsAvailable { get; }

    public VisionLicenseState LicenseState { get; }

    public VisionCapability Capabilities { get; }

    public bool Supports(VisionCapability capability) => (Capabilities & capability) == capability;
}
