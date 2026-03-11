namespace TylinkInspection.Core.Models;

public sealed class DirectoryQuery
{
    public string? ParentRegionId { get; init; }

    public bool Recursive { get; init; } = true;

    public bool ForceRefresh { get; init; }
}
