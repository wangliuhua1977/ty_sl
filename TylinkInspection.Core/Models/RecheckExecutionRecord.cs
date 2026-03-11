using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class RecheckExecutionRecord
{
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");

    public string TaskId { get; init; } = string.Empty;

    public string SourceFaultClosureId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string TriggerType { get; init; } = RecheckExecutionTriggerTypes.Scheduled;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;

    public string Outcome { get; init; } = RecheckExecutionOutcomes.Failed;

    public string TaskStatusAfter { get; init; } = RecheckTaskStatuses.Failed;

    public string FaultClosureStatusAfter { get; init; } = string.Empty;

    public int? OnlineStatus { get; init; }

    public PlaybackHealthGrade PlaybackHealthGrade { get; init; } = PlaybackHealthGrade.E;

    public string PreferredProtocol { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string FailureReason { get; init; } = string.Empty;

    public string RelatedPlaybackReviewSessionId { get; init; } = string.Empty;

    [JsonIgnore]
    public string StartedAtText => StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string CompletedAtText => CompletedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string OutcomeText => RecheckTextMapper.ToExecutionOutcomeText(Outcome);

    [JsonIgnore]
    public string TriggerTypeText => RecheckTextMapper.ToTriggerTypeText(TriggerType);

    [JsonIgnore]
    public string SummaryText => string.IsNullOrWhiteSpace(Summary) ? "--" : Summary.Trim();
}

public static class RecheckExecutionOutcomes
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Error = "Error";
    public const string Canceled = "Canceled";
    public const string Completed = "Completed";
}

public static class RecheckExecutionTriggerTypes
{
    public const string Scheduled = "Scheduled";
    public const string Manual = "Manual";
    public const string Retry = "Retry";
    public const string Recovery = "Recovery";
}
