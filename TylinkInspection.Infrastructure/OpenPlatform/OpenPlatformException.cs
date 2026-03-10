namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class OpenPlatformException : Exception
{
    public OpenPlatformException(string message, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string? ErrorCode { get; }
}
