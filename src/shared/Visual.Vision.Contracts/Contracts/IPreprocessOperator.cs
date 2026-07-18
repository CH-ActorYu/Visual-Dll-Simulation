using Visual.Image.Contracts;

namespace Visual.Vision.Contracts;

public interface IPreprocessOperator
{
    IImageLease Apply(ImageFrame source);
}
