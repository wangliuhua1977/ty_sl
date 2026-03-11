namespace TylinkInspection.Core.Models;

public sealed class ReviewSummaryReport
{
    public int PlaybackReviewSessionCount { get; init; }

    public int ScreenshotSampleCount { get; init; }

    public int ManualReviewCount { get; init; }

    public int PendingManualReviewCount { get; init; }

    public int DispatchSuggestedCount { get; init; }

    public int RecheckSuggestedCount { get; init; }

    public IReadOnlyList<ReportCountSegment> ConclusionDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportCountSegment> SourceDistribution { get; init; } = Array.Empty<ReportCountSegment>();

    public IReadOnlyList<ReportTrendPoint> TrendPoints { get; init; } = Array.Empty<ReportTrendPoint>();

    public string SummaryText { get; init; } = string.Empty;
}
