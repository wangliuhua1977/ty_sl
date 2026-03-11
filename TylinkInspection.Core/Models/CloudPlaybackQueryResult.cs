namespace TylinkInspection.Core.Models;

public sealed class CloudPlaybackQueryResult
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public DateTimeOffset QueriedAt { get; init; }

    public bool FromCache { get; init; }

    public IReadOnlyList<CloudPlaybackFile> Files { get; init; } = Array.Empty<CloudPlaybackFile>();
}
