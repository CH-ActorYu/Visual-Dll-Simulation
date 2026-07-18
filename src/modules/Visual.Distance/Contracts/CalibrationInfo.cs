using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Contracts;

public sealed record CalibrationInfo
{
    public const int CurrentSchemaVersion = 1;

    public CalibrationInfo(
        double targetPhysicalWidth,
        double referencePixelWidth,
        double referenceDistance,
        DistanceUnit unit,
        PixelMeasureAxis measureAxis,
        string cameraId,
        ImageSize imageSize,
        string detectionProfileId,
        DistanceRange validDistanceRange,
        bool distortionCorrected = false,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (!double.IsFinite(targetPhysicalWidth) || targetPhysicalWidth <= 0 ||
            !double.IsFinite(referencePixelWidth) || referencePixelWidth <= 0 ||
            !double.IsFinite(referenceDistance) || referenceDistance <= 0)
        {
            throw Invalid("Calibration dimensions and distances must be finite and positive.");
        }

        if (string.IsNullOrWhiteSpace(cameraId) || string.IsNullOrWhiteSpace(detectionProfileId))
        {
            throw Invalid("Calibration camera and detection profile IDs are required.");
        }

        if (!Enum.IsDefined(unit) || !Enum.IsDefined(measureAxis))
        {
            throw Invalid("Calibration unit or measure axis is invalid.");
        }

        if (!imageSize.IsValid || !validDistanceRange.Contains(referenceDistance))
        {
            throw Invalid("Calibration image size or validated distance range is invalid.");
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            throw Invalid($"Unsupported calibration schema version {schemaVersion}.");
        }

        TargetPhysicalWidth = targetPhysicalWidth;
        ReferencePixelWidth = referencePixelWidth;
        ReferenceDistance = referenceDistance;
        Unit = unit;
        MeasureAxis = measureAxis;
        CameraId = cameraId;
        ImageSize = imageSize;
        DetectionProfileId = detectionProfileId;
        ValidDistanceRange = validDistanceRange;
        DistortionCorrected = distortionCorrected;
        SchemaVersion = schemaVersion;
    }

    public double TargetPhysicalWidth { get; }

    public double ReferencePixelWidth { get; }

    public double ReferenceDistance { get; }

    public DistanceUnit Unit { get; }

    public PixelMeasureAxis MeasureAxis { get; }

    public string CameraId { get; }

    public ImageSize ImageSize { get; }

    public string DetectionProfileId { get; }

    public DistanceRange ValidDistanceRange { get; }

    public bool DistortionCorrected { get; }

    public int SchemaVersion { get; }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
