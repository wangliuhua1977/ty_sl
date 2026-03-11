using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskItem
{
    public string ItemId { get; init; } = Guid.NewGuid().ToString("N");

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public bool IsFocusedPoint { get; init; }

    public bool IsPendingRecheckPoint { get; init; }

    public string ExecutionStatus { get; init; } = AiInspectionTaskItemStatus.Pending;

    public int AttemptCount { get; init; }

    public string LastError { get; init; } = string.Empty;

    public string LastResultSummary { get; init; } = string.Empty;

    public bool IsAbnormalResult { get; init; }

    public string? LinkedInspectionResultId { get; init; }

    public string? LinkedReviewId { get; init; }

    public string? LinkedClosureId { get; init; }

    public string? LinkedScreenshotSampleId { get; init; }

    public string? LinkedRecheckTaskId { get; init; }

    public string? LinkedRecheckExecutionId { get; init; }

    public AiInspectionTaskItemResultSummary ResultSummary { get; init; } = new();

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    [JsonIgnore]
    public string ExecutionStatusText => AiInspectionTaskTextMapper.ToItemStatusText(ExecutionStatus);

    [JsonIgnore]
    public string AccentResourceKey => AiInspectionTaskTextMapper.ToItemAccentResourceKey(ExecutionStatus, IsAbnormalResult);

    [JsonIgnore]
    public string LastErrorText => string.IsNullOrWhiteSpace(LastError) ? "--" : LastError.Trim();

    [JsonIgnore]
    public string LastResultSummaryText => string.IsNullOrWhiteSpace(LastResultSummary) ? "--" : LastResultSummary.Trim();

    [JsonIgnore]
    public bool CanRetry =>
        string.Equals(ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase);
}
