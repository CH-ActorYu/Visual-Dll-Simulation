using Visual.Abstractions.Contracts;

namespace Visual.Distance.Internal;

internal static class DistanceConverter
{
    public static double Convert(CalibrationModel calibration, double targetPixelSize)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        if (!double.IsFinite(targetPixelSize) || targetPixelSize <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Target pixel size must be finite and positive.");
        }

        var distance = calibration.FocalPixelFactor *
                       calibration.Info.TargetPhysicalWidth /
                       targetPixelSize;
        if (!double.IsFinite(distance) || distance <= 0)
        {
            throw new VisionException(VisionErrorCode.ModuleSpecific, "Distance conversion produced an invalid value.");
        }

        return distance;
    }
}
