namespace TylinkInspection.Core.Models;

public sealed class RecheckQueueOverview
{
    public IReadOnlyList<RecheckTaskRecord> Tasks { get; init; } = Array.Empty<RecheckTaskRecord>();

    public IReadOnlyList<RecheckExecutionRecord> RecentExecutions { get; init; } = Array.Empty<RecheckExecutionRecord>();

    public int TotalCount { get; init; }

    public int EnabledCount { get; init; }

    public int DisabledCount { get; init; }

    public int DueCount { get; init; }

    public int RunningCount { get; init; }

    public int FailedCount { get; init; }

    public int CompletedCount { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    public string StatusMessage { get; init; } = string.Empty;
}
