namespace TylinkInspection.Core.Models;

public sealed class PlatformServiceException : Exception
{
    public PlatformServiceException(
        string message,
        PlatformErrorCategory category,
        string? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        ErrorCode = errorCode;
    }

    public PlatformErrorCategory Category { get; }

    public string? ErrorCode { get; }
}
