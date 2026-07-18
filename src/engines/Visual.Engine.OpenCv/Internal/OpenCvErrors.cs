using Visual.Abstractions.Contracts;

namespace Visual.Engine.OpenCv.Internal;

internal static class OpenCvErrors
{
    public static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);

    public static VisionException Decode(string message, Exception? inner = null) =>
        Create(VisionErrorCode.DecodingFailed, message, inner);

    public static VisionException Device(string message, Exception? inner = null) =>
        Create(VisionErrorCode.DeviceLost, message, inner);

    public static VisionException Module(string message, Exception? inner = null) =>
        Create(VisionErrorCode.ModuleSpecific, message, inner);

    public static VisionException Normalize(Exception exception, string message) => exception switch
    {
        VisionException visionException => visionException,
        _ => Module(message, exception)
    };

    private static VisionException Create(VisionErrorCode code, string message, Exception? inner) =>
        inner is null ? new VisionException(code, message) : new VisionException(code, message, inner);
}
