namespace TylinkInspection.Core.Models;

public sealed class ReportOverview
{
    public required ReportTimeRange TimeRange { get; init; }

    public required InspectionScopeScheme CurrentScheme { get; init; }

    public required InspectionScopeSummary ScopeSummary { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public int InspectionTotalCount { get; init; }

    public int OnlinePointCount { get; init; }

    public int OfflinePointCount { get; init; }

    public int AbnormalPointCount { get; init; }

    public int ManualReviewCount { get; init; }

    public int PendingDispatchCount { get; init; }

    public int PendingRecheckCount { get; init; }

    public int PendingClearCount { get; init; }

    public int ClosedCount { get; init; }

    public int RecheckExecutionCount { get; init; }

    public double RecheckSuccessRate { get; init; }

    public string StatusMessage { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;

    public required InspectionSummaryReport Inspection { get; init; }

    public required ReviewSummaryReport Review { get; init; }

    public required FaultClosureSummaryReport FaultClosure { get; init; }

    public required RecheckSummaryReport Recheck { get; init; }

    public required ReportExportModel ExportModel { get; init; }
}

public sealed class ReportExportModel
{
    public string SchemaVersion { get; init; } = "report-center.v1";

    public string ReportTitle { get; init; } = string.Empty;

    public required ReportTimeRange TimeRange { get; init; }

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }

    public required InspectionSummaryReport Inspection { get; init; }

    public required ReviewSummaryReport Review { get; init; }

    public required FaultClosureSummaryReport FaultClosure { get; init; }

    public required RecheckSummaryReport Recheck { get; init; }
}
