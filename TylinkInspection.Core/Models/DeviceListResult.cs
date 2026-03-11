namespace TylinkInspection.Core.Models;

public sealed class DeviceListResult
{
    public IReadOnlyList<DeviceDirectoryItem> Items { get; init; } = Array.Empty<DeviceDirectoryItem>();

    public int PageNo { get; init; } = 1;

    public int PageSize { get; init; }

    public int? TotalCount { get; init; }

    public long? NextCursor { get; init; }

    public bool HasMore { get; init; }
}
