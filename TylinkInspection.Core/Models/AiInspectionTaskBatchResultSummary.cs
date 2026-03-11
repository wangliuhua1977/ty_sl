using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskBatchResultSummary
{
    public int TotalCount { get; init; }

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public int AbnormalCount { get; init; }

    public int PendingManualReviewCount { get; init; }

    public int PendingClosureCount { get; init; }

    public int BasicInspectionResultCount { get; init; }

    public int PlaybackReviewResultCount { get; init; }

    public int ScreenshotPreparedCount { get; init; }

    public int RecheckResultCount { get; init; }

    public int PendingDispatchCount { get; init; }

    public int PendingRecheckCount { get; init; }

    public int PendingClearCount { get; init; }

    public int ClearedCount { get; init; }

    public int ClosedCount { get; init; }

    public int FalsePositiveClosedCount { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    [JsonIgnore]
    public string InspectionSummaryText => $"基础巡检 {BasicInspectionResultCount} / 异常 {AbnormalCount}";

    [JsonIgnore]
    public string PlaybackSummaryText => $"播放复核 {PlaybackReviewResultCount} / 成功 {SuccessCount}";

    [JsonIgnore]
    public string ScreenshotSummaryText => $"截图预备 {ScreenshotPreparedCount} / 待复核 {PendingManualReviewCount}";

    [JsonIgnore]
    public string RecheckSummaryText => $"复检结果 {RecheckResultCount} / 失败 {FailedCount}";

    [JsonIgnore]
    public string ClosureSummaryText =>
        $"待闭环 {PendingClosureCount} / 待派单 {PendingDispatchCount} / 待复检 {PendingRecheckCount} / 待销警 {PendingClearCount}";
}
