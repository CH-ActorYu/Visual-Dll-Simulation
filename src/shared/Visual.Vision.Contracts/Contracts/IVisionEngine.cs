namespace Visual.Vision.Contracts;

public interface IVisionEngine
{
    VisionEngineInfo Info { get; }

    ITargetDetector CreateDetector(DetectionProfile profile);

    ITracker CreateTracker(DetectionProfile profile);

    ITargetModelFactory CreateTargetModelFactory();

    IPreprocessOperator CreatePreprocessor(DetectionProfile profile);
}
