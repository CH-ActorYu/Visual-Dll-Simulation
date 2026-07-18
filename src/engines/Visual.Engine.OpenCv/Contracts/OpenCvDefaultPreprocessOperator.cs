using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public sealed class OpenCvDefaultPreprocessOperator : IPreprocessOperator
{
    private readonly OpenCvGrayscaleOperator _grayscale = new();
    private readonly OpenCvBlurOperator _blur;
    private readonly OpenCvThresholdOperator _threshold;
    private readonly OpenCvMorphologyOperator _morphology;

    public OpenCvDefaultPreprocessOperator(DetectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _blur = new OpenCvBlurOperator(profile.BlurKernelSize);
        _threshold = new OpenCvThresholdOperator(
            profile.ThresholdMode,
            profile.ManualThreshold,
            profile.ForegroundPolarity);
        _morphology = new OpenCvMorphologyOperator(profile.MorphologyKernelSize);
    }

    public IImageLease Apply(ImageFrame source)
    {
        using var grayscale = _grayscale.Apply(source);
        using var blurred = _blur.Apply(grayscale.Frame);
        using var thresholded = _threshold.Apply(blurred.Frame);
        return _morphology.Apply(thresholded.Frame);
    }
}
