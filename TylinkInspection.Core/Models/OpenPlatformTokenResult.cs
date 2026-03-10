namespace TylinkInspection.Core.Models;

public sealed class OpenPlatformTokenResult
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset ExpireAt { get; init; }

    public required DateTimeOffset RefreshExpireAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public bool IsAccessTokenExpired(DateTimeOffset now) => ExpireAt <= now;

    public bool IsRefreshTokenExpired(DateTimeOffset now) => RefreshExpireAt <= now;
}
