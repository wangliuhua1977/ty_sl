namespace TylinkInspection.Core.Models;

public sealed class ManualReviewSaveRequest
{
    public string EvidenceId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string SourceKind { get; init; } = ManualReviewSourceKinds.Live;

    public string Conclusion { get; init; } = ManualReviewConclusions.Pending;

    public string Reviewer { get; init; } = string.Empty;

    public string RemarkText { get; init; } = string.Empty;

    public bool RequiresDispatch { get; init; }

    public bool RequiresRecheck { get; init; }

    public string RelatedScreenshotSampleId { get; init; } = string.Empty;

    public string RelatedPlaybackReviewSessionId { get; init; } = string.Empty;

    public string RelatedAiAlertId { get; init; } = string.Empty;

    public string RelatedDeviceCode { get; init; } = string.Empty;
}
