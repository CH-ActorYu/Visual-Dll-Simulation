using System.Text.Json.Serialization;
using Visual.Abstractions.Contracts;

namespace Visual.Distance.Contracts;

public readonly record struct DistanceRange
{
    [JsonConstructor]
    public DistanceRange(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum <= 0 || maximum < minimum)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Distance range must be finite, positive and ordered.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public double Minimum { get; }

    public double Maximum { get; }

    public bool Contains(double value) => double.IsFinite(value) && value >= Minimum && value <= Maximum;
}
