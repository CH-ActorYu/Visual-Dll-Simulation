using Visual.Abstractions.Contracts;

namespace Visual.IO.Contracts;

public sealed record CameraCapabilities
{
    public CameraCapabilities(
        IEnumerable<ImageSize> resolutions,
        IEnumerable<double> frameRates,
        CameraParameterRange? exposure = null,
        CameraParameterRange? gain = null,
        CameraParameterRange? focus = null)
    {
        Resolutions = Array.AsReadOnly(
            (resolutions ?? throw Invalid("Camera resolutions are required.")).Distinct().ToArray());
        FrameRates = Array.AsReadOnly(
            (frameRates ?? throw Invalid("Camera frame rates are required.")).Distinct().Order().ToArray());

        if (Resolutions.Count == 0 || FrameRates.Count == 0 || FrameRates.Any(rate => !double.IsFinite(rate) || rate <= 0))
        {
            throw Invalid("Camera capabilities must contain valid resolutions and frame rates.");
        }

        Exposure = exposure;
        Gain = gain;
        Focus = focus;
    }

    public IReadOnlyList<ImageSize> Resolutions { get; }

    public IReadOnlyList<double> FrameRates { get; }

    public CameraParameterRange? Exposure { get; }

    public CameraParameterRange? Gain { get; }

    public CameraParameterRange? Focus { get; }

    public CameraParameterRange? GetRange(CameraParameter parameter) => parameter switch
    {
        CameraParameter.Exposure => Exposure,
        CameraParameter.Gain => Gain,
        CameraParameter.Focus => Focus,
        _ => null
    };

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
