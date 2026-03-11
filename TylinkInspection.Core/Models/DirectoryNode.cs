namespace TylinkInspection.Core.Models;

public sealed class DirectoryNode
{
    public string Id { get; init; } = string.Empty;

    public string? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string RegionCode { get; init; } = string.Empty;

    public string RegionGbId { get; init; } = string.Empty;

    public int Level { get; init; }

    public bool HasChildren { get; init; }

    public bool HasDevice { get; init; }

    public string FullPath { get; init; } = string.Empty;

    public List<DirectoryNode> Children { get; init; } = [];
}
