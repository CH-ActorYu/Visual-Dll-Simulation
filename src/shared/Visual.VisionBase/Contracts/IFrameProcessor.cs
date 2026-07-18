using Visual.Image.Contracts;

namespace Visual.VisionBase.Contracts;

public interface IFrameProcessor
{
    ValueTask ProcessAsync(ImageFrame frame, CancellationToken cancellationToken = default);
}
