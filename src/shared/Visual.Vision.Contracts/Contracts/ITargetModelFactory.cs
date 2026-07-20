using Visual.Image.Contracts;

namespace Visual.Vision.Contracts;

public interface ITargetModelFactory
{
    /// <summary>
    /// Creates an owned target model while borrowing <paramref name="referenceFrame"/> for this call only.
    /// The caller remains responsible for the frame lease and owns the returned model.
    /// </summary>
    ITargetModel Create(
        ImageFrame referenceFrame,
        TargetSelection selection,
        DetectionProfile profile);
}
