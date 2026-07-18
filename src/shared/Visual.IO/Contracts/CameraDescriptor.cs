using Visual.Abstractions.Contracts;

namespace Visual.IO.Contracts;

public sealed record CameraDescriptor
{
    public CameraDescriptor(
        string id,
        string name,
        string? vendor,
        string? model,
        CameraCapabilities capabilities)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Camera ID and name are required.");
        }

        Id = id;
        Name = name;
        Vendor = vendor;
        Model = model;
        Capabilities = capabilities ?? throw new VisionException(VisionErrorCode.InvalidInput, "Camera capabilities are required.");
    }

    public string Id { get; }

    public string Name { get; }

    public string? Vendor { get; }

    public string? Model { get; }

    public CameraCapabilities Capabilities { get; }
}
