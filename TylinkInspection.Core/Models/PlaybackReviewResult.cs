using System.Text.Json.Serialization;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Core.Models;

public sealed class PlaybackReviewResult
{
    public string SessionId { get; init; } = string.Empty;

    public string ReviewTargetKind { get; init; } = "Live";

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string PlaybackFileId { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public DateTimeOffset ReviewedAt { get; init; }

    public bool PlaybackStarted { get; init; }

    public bool FirstFrameVisible { get; init; }

    public int? StartupDurationMs { get; init; }

    public string UsedProtocol { get; init; } = string.Empty;

    public string UsedUrl { get; init; } = string.Empty;

    public bool UsedFallback { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public string VideoEncoding { get; init; } = string.Empty;

    [JsonIgnore]
    public string ReviewedAtText => ReviewedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string StartupDurationText => StartupDurationMs.HasValue ? $"{StartupDurationMs.Value} ms" : "--";

    [JsonIgnore]
    public string UsedUrlText => SensitiveDataMasker.MaskUrl(UsedUrl);

    [JsonIgnore]
    public string ReviewTargetText => string.Equals(ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase)
        ? "回看复核"
        : "直播复核";

    [JsonIgnore]
    public string ReviewSubjectText => string.Equals(ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase) &&
                                       !string.IsNullOrWhiteSpace(PlaybackFileName)
        ? PlaybackFileName
        : DeviceName;

    [JsonIgnore]
    public string ReviewOutcomeText => FirstFrameVisible
        ? "首帧可见"
        : PlaybackStarted
            ? "已起播但未见首帧"
            : "起播失败";

    [JsonIgnore]
    public string FailureReasonText => string.IsNullOrWhiteSpace(FailureReason) ? "无" : FailureReason.Trim();
}
