namespace TylinkInspection.Core.Models;

public sealed class InspectionSummaryReport
{
    public int CoveredPointCount { get; init; }

    public int TotalCount { get; init; }

    public int MissingInspectionCount { get; init; }

    public int OnlineCount { get; init; }

    public int OfflineCount { get; init; }

    public int AbnormalDeviceCount { get; init; }

    public int NeedRecheckCount { get; init; }

    public IReadOnlyList<ReportCountSegment> PlaybackGradeDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportTrendPoint> TrendPoints { get; init; } = Array.Empty<ReportTrendPoint>();

    public string SummaryText { get; init; } = string.Empty;
}
