namespace Visual.VisionBase.Contracts;

public enum VisionDiagnosticKind
{
    RuntimeStarted,
    RuntimeStopped,
    FrameProcessed,
    FrameDropped,
    EngineFailed,
    ConfigurationRejected
}
