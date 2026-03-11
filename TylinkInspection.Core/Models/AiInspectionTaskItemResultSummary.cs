namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskItemResultSummary
{
    public string BasicInspectionText { get; init; } = string.Empty;

    public string PlaybackReviewText { get; init; } = string.Empty;

    public string ScreenshotPreparationText { get; init; } = string.Empty;

    public string RecheckText { get; init; } = string.Empty;

    public string ManualReviewStatusText { get; init; } = string.Empty;

    public string ManualReviewStatusCode { get; init; } = string.Empty;

    public string ClosureStatusText { get; init; } = string.Empty;

    public string ClosureStatusCode { get; init; } = string.Empty;

    public bool IsPendingManualReview { get; init; }

    public bool IsPendingClosure { get; init; }

    public bool IsPendingDispatch { get; init; }

    public bool IsPendingRecheck { get; init; }

    public bool IsPendingClear { get; init; }

    public bool IsCleared { get; init; }

    public bool IsClosed { get; init; }

    public bool IsFalsePositiveClosed { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}
