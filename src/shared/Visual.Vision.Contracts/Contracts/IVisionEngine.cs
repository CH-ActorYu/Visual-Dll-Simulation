namespace Visual.Vision.Contracts;

public interface IVisionEngine
{
    VisionEngineInfo Info { get; }

    ITargetDetector CreateDetector(DetectionProfile profile);

    ITracker CreateTracker(DetectionProfile profile);

    ITargetModelFactory CreateTargetModelFactory();

    ITargetModelTracker CreateTargetModelTracker(DetectionProfile profile);

    IPreprocessOperator CreatePreprocessor(DetectionProfile profile);
}
