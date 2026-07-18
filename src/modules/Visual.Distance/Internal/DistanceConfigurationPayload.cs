using Visual.Distance.Contracts;

namespace Visual.Distance.Internal;

internal sealed record DistanceConfigurationPayload(
    string DetectionProfileId,
    CalibrationInfo? Calibration);
