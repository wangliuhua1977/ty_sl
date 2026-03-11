namespace TylinkInspection.Core.Models;

public sealed class DeviceDirectoryItem
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public int? OnlineStatus { get; init; }

    public string OnlineStatusText { get; init; } = "未知";

    public string DirectoryId { get; init; } = string.Empty;

    public string DirectoryName { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public int? DeviceSource { get; init; }

    public string DeviceSourceText { get; init; } = "未知";

    public string? Longitude { get; init; }

    public string? Latitude { get; init; }

    public string? RegionCode { get; init; }

    public string? RegionGbId { get; init; }

    public string? GbId { get; init; }

    public string? SourceGbId { get; init; }

    public string? NodeId { get; init; }

    public int? NetTypeCode { get; init; }

    public string NetTypeText { get; init; } = "未知";

    public DateTimeOffset? LastSyncedAt { get; init; }
}
