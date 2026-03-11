namespace TylinkInspection.Core.Models;

public sealed class DevicePathInfo
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceFullPath { get; init; } = string.Empty;

    public string BizFullPath { get; init; } = string.Empty;

    public List<string> ProvincialIndustryFullPaths { get; init; } = [];

    public DateTimeOffset? LastSyncedAt { get; init; }
}
