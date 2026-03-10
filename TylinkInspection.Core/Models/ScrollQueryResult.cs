namespace TylinkInspection.Core.Models;

public sealed class ScrollQueryResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int PageNo { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int? TotalCount { get; init; }

    public bool HasMore { get; init; }

    public DateTimeOffset? LastSeenTime { get; init; }

    public string? LastSeenId { get; init; }
}
