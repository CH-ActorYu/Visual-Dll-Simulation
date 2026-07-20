using Visual.Image.Contracts;

namespace Visual.Vision.Contracts;

public interface ITargetModel : IDisposable
{
    string EngineId { get; }

    string ModelVersion { get; }

    string DetectionProfileId { get; }

    TargetShape ReferenceShape { get; }

    /// <summary>
    /// Acquires a caller-owned lease for the tightly cropped BGRA preview.
    /// The returned lease must be disposed independently of this model.
    /// </summary>
    IImageLease AcquireReferencePreview();
}
