using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class RecheckTaskLatestResult
{
    public string TaskId { get; init; } = string.Empty;

    public string ExecutionId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string Outcome { get; init; } = RecheckExecutionOutcomes.Failed;

    public string TaskStatus { get; init; } = RecheckTaskStatuses.Pending;

    public string FaultClosureStatus { get; init; } = string.Empty;

    public DateTimeOffset ResultAt { get; init; } = DateTimeOffset.Now;

    public string Summary { get; init; } = string.Empty;

    public string FailureReason { get; init; } = string.Empty;

    public string PreferredProtocol { get; init; } = string.Empty;

    public int? OnlineStatus { get; init; }

    public PlaybackHealthGrade PlaybackHealthGrade { get; init; } = PlaybackHealthGrade.E;

    public string RelatedPlaybackReviewSessionId { get; init; } = string.Empty;

    [JsonIgnore]
    public string ResultAtText => ResultAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string OutcomeText => RecheckTextMapper.ToExecutionOutcomeText(Outcome);

    [JsonIgnore]
    public string SummaryText => string.IsNullOrWhiteSpace(Summary) ? "--" : Summary.Trim();
}
