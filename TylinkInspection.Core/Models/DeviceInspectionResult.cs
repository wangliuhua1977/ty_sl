using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class DeviceInspectionResult
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public int? OnlineStatus { get; init; }

    public DateTimeOffset InspectionTime { get; init; }

    public string PreferredProtocol { get; init; } = string.Empty;

    public string FallbackProtocol { get; init; } = string.Empty;

    public string PreferredUrl { get; init; } = string.Empty;

    public string FallbackUrl { get; init; } = string.Empty;

    public DateTimeOffset? ExpireTime { get; init; }

    public string VideoEnc { get; init; } = string.Empty;

    public PlaybackHealthGrade PlaybackHealthGrade { get; init; } = PlaybackHealthGrade.E;

    public string FailureReason { get; init; } = string.Empty;

    public string Suggestion { get; init; } = string.Empty;

    public bool NeedRecheck { get; init; }

    public string? ScreenshotResult { get; init; }

    public string? ReviewConclusion { get; init; }

    public string? AiLinkageStatus { get; init; }

    public string? WorkOrderStatus { get; init; }

    [JsonIgnore]
    public string OnlineStatusText => OnlineStatus switch
    {
        1 => "在线",
        0 => "离线",
        2 => "休眠",
        3 => "保活休眠",
        -1 => "不在账号下",
        _ => "未知"
    };

    [JsonIgnore]
    public string InspectionTimeText => InspectionTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string PlaybackHealthSummary => PlaybackHealthGrade switch
    {
        PlaybackHealthGrade.A => "A / 可秒开播放",
        PlaybackHealthGrade.B => "B / 可正常播放",
        PlaybackHealthGrade.C => "C / 可播但较慢",
        PlaybackHealthGrade.D => "D / 可播不稳定",
        _ => "E / 当前不可播放"
    };

    [JsonIgnore]
    public string PreferredProtocolText => ValueOrFallback(PreferredProtocol, "--");

    [JsonIgnore]
    public string FallbackProtocolText => ValueOrFallback(FallbackProtocol, "--");

    [JsonIgnore]
    public string ExpireTimeText => ExpireTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string VideoEncText => ValueOrFallback(VideoEnc, "--");

    [JsonIgnore]
    public string FailureReasonText => ValueOrFallback(FailureReason, "无");

    [JsonIgnore]
    public string FailureReasonSummary => TrimOrFallback(FailureReason, "最近无失败", 30);

    [JsonIgnore]
    public string SuggestionText => ValueOrFallback(Suggestion, "无");

    [JsonIgnore]
    public string RecheckText => NeedRecheck ? "需要复检" : "暂不需要复检";

    [JsonIgnore]
    public bool HasFailureReason => !string.IsNullOrWhiteSpace(FailureReason);

    [JsonIgnore]
    public bool HasPreferredUrl => !string.IsNullOrWhiteSpace(PreferredUrl);

    [JsonIgnore]
    public bool HasFallbackUrl => !string.IsNullOrWhiteSpace(FallbackUrl);

    [JsonIgnore]
    public bool IsAbnormal => NeedRecheck || PlaybackHealthGrade is PlaybackHealthGrade.D or PlaybackHealthGrade.E;

    private static string ValueOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string TrimOrFallback(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed[..Math.Max(0, maxLength - 1)]}…";
    }
}
