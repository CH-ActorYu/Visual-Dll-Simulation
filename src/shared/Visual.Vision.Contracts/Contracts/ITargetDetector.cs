using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Vision.Contracts;

public interface ITargetDetector
{
    TargetCandidate? Detect(ImageFrame frame, RoiRect roi);
}
