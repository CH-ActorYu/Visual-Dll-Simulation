using Visual.Abstractions.Contracts;

namespace Visual.Distance.Contracts;

public sealed class NotCalibratedException : VisionException
{
    public NotCalibratedException(string message = "The distance service is not calibrated.")
        : base(VisionErrorCode.ModuleSpecific, message)
    {
    }
}
