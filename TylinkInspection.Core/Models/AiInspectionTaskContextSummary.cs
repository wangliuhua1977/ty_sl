using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskContextSummary
{
    public string TaskId { get; init; } = string.Empty;

    public string TaskName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string TaskTypeText { get; init; } = string.Empty;

    public string SourceText { get; init; } = string.Empty;

    public string TaskStatus { get; init; } = string.Empty;

    public string TaskStatusText { get; init; } = string.Empty;

    public string TaskItemId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string ItemStatus { get; init; } = string.Empty;

    public string ItemStatusText { get; init; } = string.Empty;

    public string EvidenceId { get; init; } = string.Empty;

    public string ClosureId { get; init; } = string.Empty;

    public bool IsAbnormalResult { get; init; }

    public DateTimeOffset? ExecutedAt { get; init; }

    public DateTimeOffset? TaskStartedAt { get; init; }

    public DateTimeOffset? TaskCompletedAt { get; init; }

    public string ResultSummary { get; init; } = string.Empty;

    public string FailureSummary { get; init; } = string.Empty;

    [JsonIgnore]
    public bool HasBoundTaskItem => !string.IsNullOrWhiteSpace(TaskItemId);

    [JsonIgnore]
    public string ExecutedAtText => ExecutedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string ExecutionWindowText
    {
        get
        {
            if (TaskStartedAt.HasValue && TaskCompletedAt.HasValue)
            {
                return $"{TaskStartedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss} -> {TaskCompletedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            }

            if (TaskStartedAt.HasValue)
            {
                return TaskStartedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }

            return ExecutedAtText;
        }
    }

    [JsonIgnore]
    public string PointResultText => !string.IsNullOrWhiteSpace(ResultSummary)
        ? ResultSummary
        : !string.IsNullOrWhiteSpace(FailureSummary)
            ? FailureSummary
            : "--";

    [JsonIgnore]
    public string PlanText => !string.IsNullOrWhiteSpace(PlanName)
        ? $"{PlanName} / {PlanId}"
        : "--";

    [JsonIgnore]
    public string SourceDisplayText => !string.IsNullOrWhiteSpace(SourceText)
        ? SourceText
        : "--";

    [JsonIgnore]
    public string FailureOrAbnormalText => !string.IsNullOrWhiteSpace(FailureSummary)
        ? FailureSummary
        : IsAbnormalResult
            ? "当前点位在来源任务中判定为异常。"
            : "--";
}
