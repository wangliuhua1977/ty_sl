using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class RecheckTaskRecord
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string SourceFaultClosureId { get; init; } = string.Empty;

    public string CurrentStatus { get; init; } = RecheckTaskStatuses.Pending;

    public string ScheduleType { get; init; } = RecheckScheduleTypes.FixedDelay;

    public DateTimeOffset? NextRunAt { get; init; }

    public DateTimeOffset? LastRunAt { get; init; }

    public int RetryCount { get; init; }

    public int MaxRetryCount { get; init; } = 3;

    public bool IsEnabled { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

    public RecheckScheduleRule ScheduleRule { get; init; } = RecheckScheduleRule.CreateDefault();

    public string LastExecutionId { get; init; } = string.Empty;

    public string LastExecutionSummary { get; init; } = string.Empty;

    public string LastExecutionOutcome { get; init; } = string.Empty;

    public string LastFaultClosureStatus { get; init; } = string.Empty;

    public DateTimeOffset? LastResultAt { get; init; }

    public string LastFailureReason { get; init; } = string.Empty;

    public string LastPlaybackReviewSessionId { get; init; } = string.Empty;

    [JsonIgnore]
    public bool IsTerminalTask =>
        string.Equals(CurrentStatus, RecheckTaskStatuses.Completed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CurrentStatus, RecheckTaskStatuses.Canceled, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsRunning => string.Equals(CurrentStatus, RecheckTaskStatuses.Running, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string CurrentStatusText => RecheckTextMapper.ToTaskStatusText(CurrentStatus);

    [JsonIgnore]
    public string ScheduleTypeText => RecheckTextMapper.ToScheduleTypeText(ScheduleType);

    [JsonIgnore]
    public string NextRunAtText => NextRunAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string LastRunAtText => LastRunAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string LastResultAtText => LastResultAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string EnabledStatusText => IsEnabled ? "已启用" : "已停用";

    [JsonIgnore]
    public string LastExecutionSummaryText => string.IsNullOrWhiteSpace(LastExecutionSummary)
        ? "暂无最近执行结果"
        : LastExecutionSummary.Trim();

    [JsonIgnore]
    public string AccentResourceKey => CurrentStatus switch
    {
        RecheckTaskStatuses.Running => "TonePrimaryBrush",
        RecheckTaskStatuses.Succeeded => "ToneSuccessBrush",
        RecheckTaskStatuses.Failed => "ToneDangerBrush",
        RecheckTaskStatuses.Completed => "ToneFocusBrush",
        RecheckTaskStatuses.Canceled => "ToneInfoBrush",
        _ => "ToneWarningBrush"
    };
}

public static class RecheckTaskStatuses
{
    public const string Pending = "Pending";
    public const string Scheduled = "Scheduled";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";
    public const string Completed = "Completed";
}
