namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskDetail
{
    public required string TaskId { get; init; }

    public required string Title { get; init; }

    public required string DeviceCode { get; init; }

    public required string RegionName { get; init; }

    public required string Status { get; init; }

    public required string SourceName { get; init; }

    public required string StrategyName { get; init; }

    public required string Description { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? LatestNote { get; init; }

    public IReadOnlyList<AiInspectionExecutionRecord> ExecutionRecords { get; init; } = Array.Empty<AiInspectionExecutionRecord>();
}
