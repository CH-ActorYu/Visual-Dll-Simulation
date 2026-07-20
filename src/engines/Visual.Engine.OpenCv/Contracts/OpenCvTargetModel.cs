using OpenCvSharp;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvTargetModel : ITargetModel
{
    public const string CurrentModelVersion = "opencv-contour-template-v1";

    private Mat? _template;
    private Mat? _mask;
    private IImageLease? _referencePreview;

    internal OpenCvTargetModel(
        TargetShape referenceShape,
        string detectionProfileId,
        Mat template,
        Mat mask,
        IImageLease referencePreview)
    {
        ReferenceShape = referenceShape ?? throw new ArgumentNullException(nameof(referenceShape));
        DetectionProfileId = string.IsNullOrWhiteSpace(detectionProfileId)
            ? throw new ArgumentException("A detection profile ID is required.", nameof(detectionProfileId))
            : detectionProfileId;
        _template = template ?? throw new ArgumentNullException(nameof(template));
        _mask = mask ?? throw new ArgumentNullException(nameof(mask));
        _referencePreview = referencePreview ?? throw new ArgumentNullException(nameof(referencePreview));
    }

    public string EngineId => OpenCvEngine.EngineId;

    public string ModelVersion => CurrentModelVersion;

    public string DetectionProfileId { get; }

    public TargetShape ReferenceShape { get; }

    public IImageLease AcquireReferencePreview() =>
        (Volatile.Read(ref _referencePreview) ??
         throw new ObjectDisposedException(nameof(OpenCvTargetModel))).Retain();

    internal Mat Template =>
        Volatile.Read(ref _template) ?? throw new ObjectDisposedException(nameof(OpenCvTargetModel));

    internal Mat Mask =>
        Volatile.Read(ref _mask) ?? throw new ObjectDisposedException(nameof(OpenCvTargetModel));

    public void Dispose()
    {
        Interlocked.Exchange(ref _referencePreview, null)?.Dispose();
        Interlocked.Exchange(ref _template, null)?.Dispose();
        Interlocked.Exchange(ref _mask, null)?.Dispose();
    }
}
