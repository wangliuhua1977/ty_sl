namespace TylinkInspection.Core.Models;

public sealed class FaultClosureSummaryReport
{
    public int CurrentRecordCount { get; init; }

    public int PeriodUpdatedCount { get; init; }

    public int CurrentOpenCount { get; init; }

    public int PendingDispatchCount { get; init; }

    public int PendingRecheckCount { get; init; }

    public int PendingClearCount { get; init; }

    public int ClearedCount { get; init; }

    public int ClosedCount { get; init; }

    public int FalsePositiveClosedCount { get; init; }

    public IReadOnlyList<ReportCountSegment> StatusDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportCountSegment> SourceDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportTrendPoint> TrendPoints { get; init; } = Array.Empty<ReportTrendPoint>();

    public string SummaryText { get; init; } = string.Empty;
}
