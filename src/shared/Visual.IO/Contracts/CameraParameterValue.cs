using Visual.Abstractions.Contracts;

namespace Visual.IO.Contracts;

public readonly record struct CameraParameterValue
{
    public CameraParameterValue(double value, bool automatic = false)
    {
        if (!double.IsFinite(value))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Camera parameter value must be finite.");
        }

        Value = value;
        Automatic = automatic;
    }

    public double Value { get; }

    public bool Automatic { get; }
}
