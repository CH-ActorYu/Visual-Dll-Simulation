namespace Visual.Abstractions.Contracts;

public class VisionException : Exception
{
    public VisionException(VisionErrorCode errorCode, string message)
        : base(ValidateMessage(message))
    {
        ErrorCode = errorCode;
    }

    public VisionException(VisionErrorCode errorCode, string message, Exception innerException)
        : base(ValidateMessage(message), innerException)
    {
        ErrorCode = errorCode;
    }

    public VisionErrorCode ErrorCode { get; }

    private static string ValidateMessage(string message) =>
        string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("An exception message is required.", nameof(message))
            : message;
}
