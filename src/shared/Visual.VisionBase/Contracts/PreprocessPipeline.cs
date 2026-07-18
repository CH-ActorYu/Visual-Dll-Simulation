using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed class PreprocessPipeline
{
    private readonly object _sync = new();
    private readonly List<IPreprocessOperator> _operators = [];

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _operators.Count;
            }
        }
    }

    public PreprocessPipeline Add(IPreprocessOperator preprocessOperator)
    {
        if (preprocessOperator is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A preprocess operator is required.");
        }

        lock (_sync)
        {
            _operators.Add(preprocessOperator);
        }

        return this;
    }

    public IImageLease Run(ImageFrame source)
    {
        if (source is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "A source frame is required.");
        }

        IPreprocessOperator[] operators;
        lock (_sync)
        {
            operators = [.. _operators];
        }

        if (operators.Length == 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "The preprocess pipeline contains no operators.");
        }

        IImageLease? current = null;
        try
        {
            foreach (var preprocessOperator in operators)
            {
                IImageLease next;
                try
                {
                    next = preprocessOperator.Apply(current?.Frame ?? source)
                        ?? throw new VisionException(VisionErrorCode.ModuleSpecific, "A preprocess operator returned no output lease.");
                }
                catch (VisionException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new VisionException(VisionErrorCode.ModuleSpecific, "A preprocess operator failed.", exception);
                }

                current?.Dispose();
                current = next;
            }

            var result = current
                ?? throw new VisionException(
                    VisionErrorCode.ModuleSpecific,
                    "Preprocess pipeline produced no output lease.");
            current = null;
            return result;
        }
        finally
        {
            current?.Dispose();
        }
    }
}
