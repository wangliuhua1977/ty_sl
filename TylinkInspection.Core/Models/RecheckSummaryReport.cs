namespace TylinkInspection.Core.Models;

public sealed class RecheckSummaryReport
{
    public int TaskCount { get; init; }

    public int EnabledTaskCount { get; init; }

    public int RunningTaskCount { get; init; }

    public int ExecutionCount { get; init; }

    public int SuccessCount { get; init; }

    public int FailureCount { get; init; }

    public int ErrorCount { get; init; }

    public int CanceledCount { get; init; }

    public int CompletedCount { get; init; }

    public double SuccessRate { get; init; }

    public double FailureRate { get; init; }

    public IReadOnlyList<ReportCountSegment> OutcomeDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportCountSegment> TriggerDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportTrendPoint> TrendPoints { get; init; } = Array.Empty<ReportTrendPoint>();

    public string SummaryText { get; init; } = string.Empty;
}
