namespace TylinkInspection.Core.Models;

public sealed class DeviceListQuery
{
    public DeviceListScope Scope { get; init; } = DeviceListScope.Directory;

    public string? RegionId { get; init; }

    public string? RegionName { get; init; }

    public string? DirectoryPath { get; init; }

    public int PageNo { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public long LastId { get; init; }

    public bool ForceRefresh { get; init; }
}
