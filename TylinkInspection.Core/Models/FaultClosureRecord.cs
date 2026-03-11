using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class FaultClosureRecord
{
    public string RecordId { get; init; } = Guid.NewGuid().ToString("N");

    public string TicketNumber { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string SourceType { get; init; } = FaultClosureSourceTypes.LiveReview;

    public string FaultType { get; init; } = string.Empty;

    public string FaultSummary { get; init; } = string.Empty;

    public string ReviewConclusion { get; init; } = ManualReviewConclusions.Pending;

    public string CurrentStatus { get; init; } = FaultClosureStatuses.PendingDispatch;

    public bool RequiresDispatch { get; init; }

    public bool RequiresRecheck { get; init; }

    public bool IsFocusedPoint { get; init; }

    public string RelatedEvidenceId { get; init; } = string.Empty;

    public string RelatedManualReviewId { get; init; } = string.Empty;

    public DateTimeOffset? LastManualReviewedAt { get; init; }

    public string RelatedScreenshotSampleId { get; init; } = string.Empty;

    public string RelatedPlaybackReviewSessionId { get; init; } = string.Empty;

    public string RelatedAiAlertId { get; init; } = string.Empty;

    public string RelatedDeviceCode { get; init; } = string.Empty;

    public string PrimaryEvidenceType { get; init; } = string.Empty;

    public DateTimeOffset? PrimaryEvidenceCapturedAt { get; init; }

    public string PrimaryEvidenceImagePath { get; init; } = string.Empty;

    public string AiAlertSummary { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

    public string UpdatedBy { get; init; } = string.Empty;

    public string LastActionNote { get; init; } = string.Empty;

    public FaultDispatchDraft DispatchDraft { get; init; } = new();

    public IReadOnlyList<FaultRecheckRecord> RecheckRecords { get; init; } = Array.Empty<FaultRecheckRecord>();

    public IReadOnlyList<FaultClearRecord> ClearRecords { get; init; } = Array.Empty<FaultClearRecord>();

    [JsonIgnore]
    public bool IsTerminal => FaultClosureStatuses.IsTerminal(CurrentStatus);

    [JsonIgnore]
    public bool IsPendingDispatch => string.Equals(CurrentStatus, FaultClosureStatuses.PendingDispatch, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAwaitingRecheck => FaultClosureStateMachine.ShouldAppearInRecheckQueue(this);

    [JsonIgnore]
    public bool CanMarkDispatched => FaultClosureStateMachine.CanMarkDispatched(CurrentStatus);

    [JsonIgnore]
    public bool CanRunRecheck => FaultClosureStateMachine.CanRunRecheck(this);

    [JsonIgnore]
    public bool CanClear => FaultClosureStateMachine.CanClear(CurrentStatus);

    [JsonIgnore]
    public bool CanClose => FaultClosureStateMachine.CanClose(CurrentStatus);

    [JsonIgnore]
    public string StatusText => FaultClosureTextMapper.ToStatusText(CurrentStatus);

    [JsonIgnore]
    public string SourceTypeText => FaultClosureTextMapper.ToSourceText(SourceType);

    [JsonIgnore]
    public string ReviewConclusionText => ManualReviewTextMapper.ToConclusionText(ReviewConclusion);

    [JsonIgnore]
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string LastManualReviewedAtText => LastManualReviewedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string PrimaryEvidenceCapturedAtText => PrimaryEvidenceCapturedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string AccentResourceKey => CurrentStatus switch
    {
        FaultClosureStatuses.PendingDispatch => "ToneWarningBrush",
        FaultClosureStatuses.DispatchedPendingProcess => "TonePrimaryBrush",
        FaultClosureStatuses.PendingRecheck => "ToneDangerBrush",
        FaultClosureStatuses.RecheckPassedPendingClear => "ToneFocusBrush",
        FaultClosureStatuses.Cleared => "ToneSuccessBrush",
        FaultClosureStatuses.FalsePositiveClosed => "ToneFocusBrush",
        FaultClosureStatuses.Closed => "ToneInfoBrush",
        _ => "ToneInfoBrush"
    };

    [JsonIgnore]
    public bool HasEvidenceImage => !string.IsNullOrWhiteSpace(PrimaryEvidenceImagePath);

    [JsonIgnore]
    public string PendingFlagsText
    {
        get
        {
            var segments = new List<string>();

            if (IsPendingDispatch)
            {
                segments.Add("待派单");
            }

            if (IsAwaitingRecheck)
            {
                segments.Add("待复检");
            }

            if (IsFocusedPoint)
            {
                segments.Add("重点点位");
            }

            return segments.Count == 0 ? "无额外标记" : string.Join(" / ", segments);
        }
    }

    [JsonIgnore]
    public string EvidenceSummaryText
    {
        get
        {
            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(RelatedScreenshotSampleId))
            {
                segments.Add("截图");
            }

            if (!string.IsNullOrWhiteSpace(RelatedPlaybackReviewSessionId))
            {
                segments.Add("回看/播放复核");
            }

            if (!string.IsNullOrWhiteSpace(RelatedAiAlertId))
            {
                segments.Add("AI告警");
            }

            return segments.Count == 0 ? "当前点位" : string.Join(" / ", segments);
        }
    }

    [JsonIgnore]
    public string LatestRecheckText => RecheckRecords.Count == 0
        ? "未复检"
        : RecheckRecords.OrderByDescending(item => item.TriggeredAt).First().ResultSummaryText;
}

public sealed class FaultDispatchDraft
{
    public string TicketNumber { get; init; } = string.Empty;

    public string SiteName { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string FaultType { get; init; } = string.Empty;

    public string FaultSummary { get; init; } = string.Empty;

    public string ManualReviewConclusion { get; init; } = ManualReviewConclusions.Pending;

    public string EvidenceSummary { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public string CreatedBy { get; init; } = string.Empty;

    public string CurrentStatus { get; init; } = FaultClosureStatuses.PendingDispatch;

    public DateTimeOffset? DispatchedAt { get; init; }

    public string DispatchedBy { get; init; } = string.Empty;

    public IReadOnlyList<string> ReservedChannels { get; init; } =
    [
        "EnterpriseWeComWebhook",
        "QuantumSecretWebhook"
    ];

    [JsonIgnore]
    public bool HasDraft => !string.IsNullOrWhiteSpace(TicketNumber);

    [JsonIgnore]
    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string DispatchedAtText => DispatchedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string CurrentStatusText => FaultClosureTextMapper.ToStatusText(CurrentStatus);

    [JsonIgnore]
    public string ManualReviewConclusionText => ManualReviewTextMapper.ToConclusionText(ManualReviewConclusion);

    [JsonIgnore]
    public string ReservedChannelsText => ReservedChannels.Count == 0
        ? "未预留"
        : string.Join(" / ", ReservedChannels);
}

public sealed class FaultRecheckRecord
{
    public string RecheckId { get; init; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset TriggeredAt { get; init; } = DateTimeOffset.Now;

    public string TriggeredBy { get; init; } = string.Empty;

    public DateTimeOffset InspectionTime { get; init; } = DateTimeOffset.Now;

    public string Outcome { get; init; } = FaultRecheckOutcomes.Failed;

    public int? OnlineStatus { get; init; }

    public PlaybackHealthGrade PlaybackHealthGrade { get; init; } = PlaybackHealthGrade.E;

    public string PreferredProtocol { get; init; } = string.Empty;

    public string FailureReason { get; init; } = string.Empty;

    public string Suggestion { get; init; } = string.Empty;

    public string RelatedPlaybackReviewSessionId { get; init; } = string.Empty;

    public string RelatedScreenshotSampleId { get; init; } = string.Empty;

    [JsonIgnore]
    public string TriggeredAtText => TriggeredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string OutcomeText => FaultClosureTextMapper.ToRecheckOutcomeText(Outcome);

    [JsonIgnore]
    public string ResultSummaryText => Outcome switch
    {
        FaultRecheckOutcomes.Passed => $"复检通过 / {InspectionTime.ToLocalTime():MM-dd HH:mm}",
        _ => $"复检失败 / {InspectionTime.ToLocalTime():MM-dd HH:mm} / {PlaybackHealthGrade}"
    };
}

public sealed class FaultClearRecord
{
    public string ClearId { get; init; } = Guid.NewGuid().ToString("N");

    public string ActionType { get; init; } = FaultClearActionTypes.Close;

    public DateTimeOffset PerformedAt { get; init; } = DateTimeOffset.Now;

    public string PerformedBy { get; init; } = string.Empty;

    public string StatusBefore { get; init; } = string.Empty;

    public string StatusAfter { get; init; } = string.Empty;

    public string NoteText { get; init; } = string.Empty;

    [JsonIgnore]
    public string PerformedAtText => PerformedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string ActionTypeText => FaultClosureTextMapper.ToClearActionText(ActionType);

    [JsonIgnore]
    public string StatusAfterText => FaultClosureTextMapper.ToStatusText(StatusAfter);
}

public static class FaultClosureSourceTypes
{
    public const string LiveReview = "LiveReview";
    public const string PlaybackReview = "PlaybackReview";
    public const string AiAlert = "AiAlert";
    public const string InspectionFailure = "InspectionFailure";
}

public static class FaultClosureEvidenceTypes
{
    public const string Screenshot = "Screenshot";
    public const string AiSnapshot = "AiSnapshot";
    public const string InspectionResult = "InspectionResult";
}

public static class FaultClosureStatuses
{
    public const string PendingDispatch = "PendingDispatch";
    public const string DispatchedPendingProcess = "DispatchedPendingProcess";
    public const string PendingRecheck = "PendingRecheck";
    public const string RecheckPassedPendingClear = "RecheckPassedPendingClear";
    public const string Cleared = "Cleared";
    public const string Closed = "Closed";
    public const string FalsePositiveClosed = "FalsePositiveClosed";

    public static bool IsTerminal(string? status)
    {
        return string.Equals(status, Cleared, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, Closed, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, FalsePositiveClosed, StringComparison.OrdinalIgnoreCase);
    }
}

public static class FaultRecheckOutcomes
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
}

public static class FaultClearActionTypes
{
    public const string ClearAlarm = "ClearAlarm";
    public const string Close = "Close";
    public const string FalsePositiveClose = "FalsePositiveClose";
}

public static class FaultClosureTextMapper
{
    public static string ToStatusText(string? status)
    {
        return status switch
        {
            FaultClosureStatuses.PendingDispatch => "待派单",
            FaultClosureStatuses.DispatchedPendingProcess => "已派单待处理",
            FaultClosureStatuses.PendingRecheck => "待复检",
            FaultClosureStatuses.RecheckPassedPendingClear => "复检通过待销警",
            FaultClosureStatuses.Cleared => "已销警",
            FaultClosureStatuses.Closed => "已关闭",
            FaultClosureStatuses.FalsePositiveClosed => "误报关闭",
            _ => "未知状态"
        };
    }

    public static string ToSourceText(string? sourceType)
    {
        return sourceType switch
        {
            FaultClosureSourceTypes.LiveReview => "直播复核",
            FaultClosureSourceTypes.PlaybackReview => "回看复核",
            FaultClosureSourceTypes.AiAlert => "AI画面异常",
            FaultClosureSourceTypes.InspectionFailure => "基础巡检失败",
            _ => "未知来源"
        };
    }

    public static string ToRecheckOutcomeText(string? outcome)
    {
        return outcome switch
        {
            FaultRecheckOutcomes.Passed => "复检通过",
            FaultRecheckOutcomes.Failed => "复检失败",
            _ => "未执行"
        };
    }

    public static string ToClearActionText(string? actionType)
    {
        return actionType switch
        {
            FaultClearActionTypes.ClearAlarm => "人工确认恢复销警",
            FaultClearActionTypes.FalsePositiveClose => "误报关闭",
            FaultClearActionTypes.Close => "手动关闭",
            _ => "本地闭环动作"
        };
    }
}
