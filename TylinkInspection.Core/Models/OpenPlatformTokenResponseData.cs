namespace TylinkInspection.Core.Models;

public sealed class OpenPlatformTokenResponseData
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required int ExpiresIn { get; init; }

    public required int RefreshExpiresIn { get; init; }
}
