namespace TylinkInspection.Core.Models;

public sealed class FaultClosureOverview
{
    public IReadOnlyList<FaultClosureRecord> Records { get; init; } = Array.Empty<FaultClosureRecord>();

    public IReadOnlyList<string> FaultTypes { get; init; } = Array.Empty<string>();

    public int TotalCount { get; init; }

    public int PendingDispatchCount { get; init; }

    public int PendingRecheckCount { get; init; }

    public int PendingClearCount { get; init; }

    public int ClearedCount { get; init; }

    public int ClosedCount { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    public string StatusMessage { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;
}
