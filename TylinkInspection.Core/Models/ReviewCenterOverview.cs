namespace TylinkInspection.Core.Models;

public sealed class ReviewCenterOverview
{
    public required InspectionScopeScheme CurrentScheme { get; init; }

    public required InspectionScopeSummary ScopeSummary { get; init; }

    public required IReadOnlyList<ReviewEvidenceItem> EvidenceItems { get; init; }

    public required int TotalEvidenceCount { get; init; }

    public required int PendingManualCount { get; init; }

    public required int AbnormalEvidenceCount { get; init; }

    public required int AiEvidenceCount { get; init; }

    public required int FocusedEvidenceCount { get; init; }

    public required int ContinuousAbnormalPointCount { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }

    public string StatusMessage { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;
}
