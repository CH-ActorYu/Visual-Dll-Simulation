namespace Visual.VisionBase.Contracts;

public interface IVisionDiagnosticSink
{
    void Write(VisionDiagnosticRecord diagnostic);
}
