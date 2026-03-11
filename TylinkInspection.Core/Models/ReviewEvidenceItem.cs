using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class ReviewEvidenceItem
{
    public string EvidenceId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string DirectoryName { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public string EvidenceKind { get; init; } = ReviewEvidenceKinds.LiveScreenshot;

    public string SourceKind { get; init; } = ManualReviewSourceKinds.Live;

    public string EvidenceRole { get; init; } = ReviewEvidenceRoles.Current;

    public string ImageUri { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public string ReviewSessionId { get; init; } = string.Empty;

    public string PlaybackFileId { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public string AiAlertId { get; init; } = string.Empty;

    public string AiAlertMsgId { get; init; } = string.Empty;

    public string AiAlertContent { get; init; } = string.Empty;

    public string AiAlertSourceName { get; init; } = string.Empty;

    public PlaybackHealthGrade? PlaybackHealthGrade { get; init; }

    public string VideoEncoding { get; init; } = string.Empty;

    public string FailureReason { get; init; } = string.Empty;

    public bool IsFocused { get; init; }

    public bool NeedRecheck { get; init; }

    public bool IsMachineAbnormal { get; init; }

    public bool IsAbnormal { get; init; }

    public bool IsPendingManualReview { get; init; }

    public bool IsContinuousAbnormal { get; init; }

    public string ManualReviewConclusion { get; init; } = ManualReviewConclusions.Pending;

    public string ManualReviewReviewer { get; init; } = string.Empty;

    public DateTimeOffset? ManualReviewedAt { get; init; }

    public string ManualReviewRemark { get; init; } = string.Empty;

    public bool RequiresDispatch { get; init; }

    public bool RequiresRecheck { get; init; }

    [JsonIgnore]
    public string CapturedAtText => CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string EvidenceKindText => EvidenceKind switch
    {
        ReviewEvidenceKinds.LiveScreenshot => "直播截图",
        ReviewEvidenceKinds.PlaybackScreenshot => "回看截图",
        ReviewEvidenceKinds.AiEvidence => "AI证据图",
        _ => "未知来源"
    };

    [JsonIgnore]
    public string EvidenceRoleText => string.Equals(EvidenceRole, ReviewEvidenceRoles.Current, StringComparison.OrdinalIgnoreCase)
        ? "当前截图"
        : "历史截图";

    [JsonIgnore]
    public string SourceKindText => ManualReviewTextMapper.ToSourceText(SourceKind);

    [JsonIgnore]
    public string ManualReviewConclusionText => ManualReviewTextMapper.ToConclusionText(ManualReviewConclusion);

    [JsonIgnore]
    public string ManualReviewedAtText => ManualReviewedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string PlaybackGradeText => PlaybackHealthGrade.HasValue
        ? $"等级 {PlaybackHealthGrade.Value}"
        : "待体检";

    [JsonIgnore]
    public string RegionAndSchemeText => string.IsNullOrWhiteSpace(DirectoryName)
        ? SchemeName
        : $"{DirectoryName} / {SchemeName}";

    [JsonIgnore]
    public string AbnormalTagText
    {
        get
        {
            if (IsPendingManualReview)
            {
                return "待人工复核";
            }

            if (IsContinuousAbnormal)
            {
                return "连续异常";
            }

            if (IsAbnormal)
            {
                return "画面异常";
            }

            return "状态稳定";
        }
    }

    [JsonIgnore]
    public string AccentResourceKey
    {
        get
        {
            if (IsPendingManualReview)
            {
                return "ToneWarningBrush";
            }

            if (IsContinuousAbnormal)
            {
                return "ToneDangerBrush";
            }

            if (string.Equals(EvidenceKind, ReviewEvidenceKinds.AiEvidence, StringComparison.OrdinalIgnoreCase))
            {
                return IsAbnormal ? "ToneDangerBrush" : "ToneFocusBrush";
            }

            if (IsFocused)
            {
                return "ToneFocusBrush";
            }

            return IsAbnormal ? "ToneWarningBrush" : "ToneSuccessBrush";
        }
    }

    [JsonIgnore]
    public bool HasImageUri => !string.IsNullOrWhiteSpace(ImageUri);
}

public static class ReviewEvidenceKinds
{
    public const string LiveScreenshot = "LiveScreenshot";
    public const string PlaybackScreenshot = "PlaybackScreenshot";
    public const string AiEvidence = "AiEvidence";
}

public static class ReviewEvidenceRoles
{
    public const string Current = "Current";
    public const string Historical = "Historical";
}
