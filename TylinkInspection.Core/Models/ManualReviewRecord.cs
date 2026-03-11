using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class ManualReviewRecord
{
    public string ReviewId { get; init; } = Guid.NewGuid().ToString("N");

    public string EvidenceId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string SourceKind { get; init; } = ManualReviewSourceKinds.Live;

    public string Conclusion { get; init; } = ManualReviewConclusions.Pending;

    public string Reviewer { get; init; } = string.Empty;

    public DateTimeOffset ReviewedAt { get; init; } = DateTimeOffset.Now;

    public string RemarkText { get; init; } = string.Empty;

    public bool RequiresDispatch { get; init; }

    public bool RequiresRecheck { get; init; }

    public string RelatedScreenshotSampleId { get; init; } = string.Empty;

    public string RelatedPlaybackReviewSessionId { get; init; } = string.Empty;

    public string RelatedAiAlertId { get; init; } = string.Empty;

    public string RelatedDeviceCode { get; init; } = string.Empty;

    [JsonIgnore]
    public string ReviewedAtText => ReviewedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string SourceKindText => ManualReviewTextMapper.ToSourceText(SourceKind);

    [JsonIgnore]
    public string ConclusionText => ManualReviewTextMapper.ToConclusionText(Conclusion);

    [JsonIgnore]
    public bool IsPending => string.Equals(Conclusion, ManualReviewConclusions.Pending, StringComparison.OrdinalIgnoreCase);
}

public static class ManualReviewSourceKinds
{
    public const string Live = "Live";
    public const string Playback = "Playback";
    public const string Ai = "AI";
}

public static class ManualReviewConclusions
{
    public const string Pending = "Pending";
    public const string Normal = "Normal";
    public const string BlackScreen = "BlackScreen";
    public const string FrozenFrame = "FrozenFrame";
    public const string Tilted = "Tilted";
    public const string Obstruction = "Obstruction";
    public const string Blur = "Blur";
    public const string LowLight = "LowLight";
    public const string FalsePositive = "FalsePositive";
}

public static class ManualReviewTextMapper
{
    public static string ToSourceText(string? sourceKind)
    {
        return sourceKind switch
        {
            ManualReviewSourceKinds.Live => "直播",
            ManualReviewSourceKinds.Playback => "回看",
            ManualReviewSourceKinds.Ai => "AI",
            _ => "未知"
        };
    }

    public static string ToConclusionText(string? conclusion)
    {
        return conclusion switch
        {
            ManualReviewConclusions.Pending => "待复核",
            ManualReviewConclusions.Normal => "正常",
            ManualReviewConclusions.BlackScreen => "黑屏",
            ManualReviewConclusions.FrozenFrame => "冻帧",
            ManualReviewConclusions.Tilted => "偏斜",
            ManualReviewConclusions.Obstruction => "遮挡",
            ManualReviewConclusions.Blur => "模糊",
            ManualReviewConclusions.LowLight => "低照度",
            ManualReviewConclusions.FalsePositive => "误报",
            _ => "未知"
        };
    }
}
