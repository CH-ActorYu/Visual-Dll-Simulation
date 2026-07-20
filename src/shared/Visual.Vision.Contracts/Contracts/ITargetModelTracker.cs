using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Vision.Contracts;

public interface ITargetModelTracker : IDisposable
{
    /// <summary>
    /// Initializes tracking from a borrowed frame and model. The tracker does not own either argument.
    /// </summary>
    void Initialize(ImageFrame frame, ITargetModel model, RoiRect? searchRegion = null);

    TargetTrackingResult Update(ImageFrame frame, RoiRect? searchRegion = null);
}
