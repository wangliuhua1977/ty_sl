using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionFailureDashboard
{
    public IReadOnlyList<AiInspectionFailedPlanSummary> FailedPlans { get; init; } = Array.Empty<AiInspectionFailedPlanSummary>();

    public IReadOnlyList<AiInspectionFailedBatchSummary> FailedBatches { get; init; } = Array.Empty<AiInspectionFailedBatchSummary>();

    public IReadOnlyList<AiInspectionFailedPointSummary> FailedPoints { get; init; } = Array.Empty<AiInspectionFailedPointSummary>();

    public IReadOnlyList<AiInspectionFailureReasonStat> FailureReasons { get; init; } = Array.Empty<AiInspectionFailureReasonStat>();

    public IReadOnlyList<AiInspectionContinuousFailurePointSummary> RepeatedFailurePoints { get; init; } = Array.Empty<AiInspectionContinuousFailurePointSummary>();

    public IReadOnlyList<AiInspectionTaskTypeFailureStat> TaskTypeFailures { get; init; } = Array.Empty<AiInspectionTaskTypeFailureStat>();

    [JsonIgnore]
    public string MostFailedPlanText => FailedPlans.FirstOrDefault()?.HeadlineText ?? "--";

    [JsonIgnore]
    public string MostFailedTaskTypeText => TaskTypeFailures.FirstOrDefault()?.HeadlineText ?? "--";

    [JsonIgnore]
    public string MostRepeatedPointText => RepeatedFailurePoints.FirstOrDefault()?.HeadlineText ?? "--";
}

public sealed record class AiInspectionFailedPlanSummary
{
    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string TaskTypeText { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public int FailedBatchCount { get; init; }

    public int FailedPointCount { get; init; }

    public DateTimeOffset? LastFailedAt { get; init; }

    public string LatestFailureSummary { get; init; } = string.Empty;

    [JsonIgnore]
    public string LastFailedAtText => LastFailedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string HeadlineText => $"{PlanName} / 失败批次 {FailedBatchCount} / 失败点位 {FailedPointCount}";
}

public sealed record class AiInspectionFailedBatchSummary
{
    public string TaskId { get; init; } = string.Empty;

    public string TaskName { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string TaskTypeText { get; init; } = string.Empty;

    public string SourceText { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public int FailedCount { get; init; }

    public int AbnormalCount { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }

    public string FailureSummary { get; init; } = string.Empty;

    [JsonIgnore]
    public string OccurredAtText => OccurredAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
}

public sealed record class AiInspectionFailedPointSummary
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string TaskName { get; init; } = string.Empty;

    public string TaskItemId { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string TaskTypeText { get; init; } = string.Empty;

    public int AttemptCount { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public string ResultSummary { get; init; } = string.Empty;

    [JsonIgnore]
    public string OccurredAtText => OccurredAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
}

public sealed record class AiInspectionFailureReasonStat
{
    public string CategoryName { get; init; } = string.Empty;

    public int FailedItemCount { get; init; }

    public int AffectedPointCount { get; init; }

    public DateTimeOffset? LastOccurredAt { get; init; }

    [JsonIgnore]
    public string LastOccurredAtText => LastOccurredAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
}

public sealed record class AiInspectionContinuousFailurePointSummary
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public int FailedTaskCount { get; init; }

    public int ConsecutiveFailureCount { get; init; }

    public DateTimeOffset? LastFailedAt { get; init; }

    public string LatestFailureReason { get; init; } = string.Empty;

    [JsonIgnore]
    public string LastFailedAtText => LastFailedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string HeadlineText => $"{DeviceName} / 连续失败 {ConsecutiveFailureCount} / 累计失败 {FailedTaskCount}";
}

public sealed record class AiInspectionTaskTypeFailureStat
{
    public string TaskType { get; init; } = string.Empty;

    public string TaskTypeText { get; init; } = string.Empty;

    public int FailedBatchCount { get; init; }

    public int FailedItemCount { get; init; }

    [JsonIgnore]
    public string HeadlineText => $"{TaskTypeText} / 失败批次 {FailedBatchCount} / 失败点位 {FailedItemCount}";
}
