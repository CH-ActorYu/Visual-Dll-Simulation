using System.Text.Json;

namespace Visual.Abstractions.Contracts;

public sealed record ModuleConfiguration
{
    public ModuleConfiguration(int schemaVersion, string engineId, string json)
    {
        if (schemaVersion <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Configuration schema version must be positive.");
        }

        if (string.IsNullOrWhiteSpace(engineId))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Configuration engine ID is required.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Configuration JSON is required.");
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Configuration JSON is invalid.", exception);
        }

        SchemaVersion = schemaVersion;
        EngineId = engineId;
        Json = json;
    }

    public int SchemaVersion { get; }

    public string EngineId { get; }

    public string Json { get; }
}
