namespace TylinkInspection.Core.Models;

public sealed class OpenPlatformTokenState
{
    public required string AppId { get; init; }

    public required string EnterpriseUser { get; init; }

    public bool HasToken { get; init; }

    public bool CanReuseAccessToken { get; init; }

    public bool CanRefreshToken { get; init; }

    public string MaskedAccessToken { get; init; } = "--";

    public string MaskedRefreshToken { get; init; } = "--";

    public DateTimeOffset? ExpireAt { get; init; }

    public DateTimeOffset? RefreshExpireAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public string Summary { get; init; } = string.Empty;
}
