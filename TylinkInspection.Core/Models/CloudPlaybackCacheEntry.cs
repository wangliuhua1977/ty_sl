namespace TylinkInspection.Core.Models;

public sealed class CloudPlaybackCacheEntry
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public DateTimeOffset CachedAt { get; init; }

    public IReadOnlyList<CloudPlaybackFile> Files { get; init; } = Array.Empty<CloudPlaybackFile>();
}
