namespace Visual.Vision.Contracts;

public interface ITargetModel : IDisposable
{
    string EngineId { get; }

    string ModelVersion { get; }

    TargetShape ReferenceShape { get; }
}
