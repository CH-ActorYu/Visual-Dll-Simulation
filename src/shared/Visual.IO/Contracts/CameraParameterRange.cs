using Visual.Abstractions.Contracts;

namespace Visual.IO.Contracts;

public sealed record CameraParameterRange
{
    public CameraParameterRange(double minimum, double maximum, double step, bool supportsAuto)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || !double.IsFinite(step) ||
            minimum > maximum || step <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Camera parameter range is invalid.");
        }

        Minimum = minimum;
        Maximum = maximum;
        Step = step;
        SupportsAuto = supportsAuto;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public bool SupportsAuto { get; }

    public bool Contains(double value) => double.IsFinite(value) && value >= Minimum && value <= Maximum;
}
