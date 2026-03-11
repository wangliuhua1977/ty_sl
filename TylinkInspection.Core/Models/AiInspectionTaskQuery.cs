namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskQuery
{
    public string? Keyword { get; init; }

    public string? Status { get; init; }

    public string? TaskType { get; init; }

    public string? ScopeMode { get; init; }

    public string? SchemeId { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}
