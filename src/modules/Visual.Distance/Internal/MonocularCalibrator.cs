using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;

namespace Visual.Distance.Internal;

internal static class MonocularCalibrator
{
    public static CalibrationModel Create(CalibrationInfo calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        var focalPixelFactor = calibration.ReferencePixelWidth *
                               calibration.ReferenceDistance /
                               calibration.TargetPhysicalWidth;
        if (!double.IsFinite(focalPixelFactor) || focalPixelFactor <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Calibration produced an invalid focal pixel factor.");
        }

        return new CalibrationModel(calibration, focalPixelFactor);
    }
}

internal sealed record CalibrationModel(CalibrationInfo Info, double FocalPixelFactor);
