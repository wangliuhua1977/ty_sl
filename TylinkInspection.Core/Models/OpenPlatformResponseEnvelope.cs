namespace TylinkInspection.Core.Models;

public sealed class OpenPlatformResponseEnvelope<T>
{
    public int Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public bool Success => Code == 0;
}
