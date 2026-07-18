namespace Visual.Vision.Contracts;

public interface IVisionEngineProvider
{
    IReadOnlyList<VisionEngineInfo> GetAvailableEngines();

    IVisionEngine Get(string id);
}
