using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Vision.Contracts;

public interface ITracker : IDisposable
{
    void Initialize(ImageFrame frame, RoiRect roi);

    TargetCandidate? Update(ImageFrame frame);
}
