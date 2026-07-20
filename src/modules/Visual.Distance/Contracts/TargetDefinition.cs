using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Contracts;

public sealed record TargetDefinition
{
    public const int CurrentSchemaVersion = 1;

    public TargetDefinition(
        string targetId,
        string referenceResourceId,
        string referenceContentHash,
        double physicalSize,
        DistanceUnit unit,
        PixelMeasureAxis measureAxis,
        string engineId,
        string modelVersion,
        string detectionProfileId,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (string.IsNullOrWhiteSpace(targetId) ||
            string.IsNullOrWhiteSpace(referenceResourceId) ||
            string.IsNullOrWhiteSpace(referenceContentHash))
        {
            throw Invalid("Target ID, reference resource ID and content hash are required.");
        }

        if (!double.IsFinite(physicalSize) || physicalSize <= 0)
        {
            throw Invalid("Target physical size must be positive and finite.");
        }

        if (!Enum.IsDefined(unit) || !Enum.IsDefined(measureAxis))
        {
            throw Invalid("Target distance unit or measurement axis is invalid.");
        }

        if (string.IsNullOrWhiteSpace(engineId) ||
            string.IsNullOrWhiteSpace(modelVersion) ||
            string.IsNullOrWhiteSpace(detectionProfileId))
        {
            throw Invalid("Target engine, model version and detection profile ID are required.");
        }

        if (schemaVersion != CurrentSchemaVersion)
        {
            throw Invalid($"Unsupported target definition schema version {schemaVersion}.");
        }

        TargetId = targetId;
        ReferenceResourceId = referenceResourceId;
        ReferenceContentHash = referenceContentHash;
        PhysicalSize = physicalSize;
        Unit = unit;
        MeasureAxis = measureAxis;
        EngineId = engineId;
        ModelVersion = modelVersion;
        DetectionProfileId = detectionProfileId;
        SchemaVersion = schemaVersion;
    }

    public string TargetId { get; }

    public string ReferenceResourceId { get; }

    public string ReferenceContentHash { get; }

    public double PhysicalSize { get; }

    public DistanceUnit Unit { get; }

    public PixelMeasureAxis MeasureAxis { get; }

    public string EngineId { get; }

    public string ModelVersion { get; }

    public string DetectionProfileId { get; }

    public int SchemaVersion { get; }

    public static TargetDefinition FromModel(
        string targetId,
        string referenceResourceId,
        string referenceContentHash,
        double physicalSize,
        DistanceUnit unit,
        ITargetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return new TargetDefinition(
            targetId,
            referenceResourceId,
            referenceContentHash,
            physicalSize,
            unit,
            model.ReferenceShape.MeasureAxis,
            model.EngineId,
            model.ModelVersion,
            model.DetectionProfileId);
    }

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
