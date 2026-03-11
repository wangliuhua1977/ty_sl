using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskPlanExecutionHistory
{
    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string TaskTypeText { get; init; } = string.Empty;

    public string ScopeMode { get; init; } = string.Empty;

    public string ScopeModeText { get; init; } = string.Empty;

    public string ScheduleType { get; init; } = string.Empty;

    public string ScheduleText { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public DateTimeOffset? LastRunAt { get; init; }

    public string LatestTaskId { get; init; } = string.Empty;

    public string LatestTaskName { get; init; } = string.Empty;

    public string LatestTaskStatus { get; init; } = string.Empty;

    public string LatestTaskStatusText { get; init; } = string.Empty;

    public string LatestResultSummary { get; init; } = string.Empty;

    public int SuccessCount { get; init; }

    public int FailedCount { get; init; }

    public int AbnormalCount { get; init; }

    public int PendingManualReviewCount { get; init; }

    public int PendingClosureCount { get; init; }

    public int ExecutedBatchCount { get; init; }

    public int FailedBatchCount { get; init; }

    [JsonIgnore]
    public string LastRunAtText => LastRunAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string LatestTaskText => string.IsNullOrWhiteSpace(LatestTaskName)
        ? "--"
        : $"{LatestTaskName} / {LatestTaskStatusText}";

    [JsonIgnore]
    public string CountersText => $"成功 {SuccessCount} / 失败 {FailedCount} / 异常 {AbnormalCount}";

    [JsonIgnore]
    public string PendingText => $"待复核 {PendingManualReviewCount} / 待闭环 {PendingClosureCount}";

    [JsonIgnore]
    public string EnabledText => IsEnabled ? "启用中" : "已停用";
}
