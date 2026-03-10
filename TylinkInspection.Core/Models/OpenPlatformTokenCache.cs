namespace TylinkInspection.Core.Models;

public sealed class OpenPlatformTokenCache
{
    public required string AppId { get; init; }

    public required string EnterpriseUser { get; init; }

    public string? AccessToken { get; init; }

    public string? RefreshToken { get; init; }

    public DateTimeOffset? ExpireAt { get; init; }

    public DateTimeOffset? RefreshExpireAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }
}
