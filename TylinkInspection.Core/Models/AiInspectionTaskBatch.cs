using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskBatch
{
    public string TaskId { get; init; } = Guid.NewGuid().ToString("N");

    public string TaskName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string SourceKind { get; init; } = AiInspectionTaskBatchSourceKind.Manual;

    public string SourcePlanId { get; init; } = string.Empty;

    public string SourcePlanName { get; init; } = string.Empty;

    public string ParentTaskId { get; init; } = string.Empty;

    public string ParentTaskName { get; init; } = string.Empty;

    public string TaskType { get; init; } = AiInspectionTaskType.BasicInspection;

    public string ScopeMode { get; init; } = AiInspectionTaskScopeMode.FullScheme;

    public int TotalCount { get; init; }

    public int SucceededCount { get; init; }

    public int FailedCount { get; init; }

    public int AbnormalCount { get; init; }

    public int CanceledCount { get; init; }

    public string Status { get; init; } = AiInspectionTaskStatus.Pending;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public string FailureSummary { get; init; } = string.Empty;

    public string LatestResultSummary { get; init; } = string.Empty;

    public AiInspectionTaskBatchResultSummary ResultSummary { get; init; } = new();

    public IReadOnlyList<AiInspectionTaskItem> Items { get; init; } = Array.Empty<AiInspectionTaskItem>();

    public IReadOnlyList<AiInspectionTaskExecutionRecord> ExecutionRecords { get; init; } = Array.Empty<AiInspectionTaskExecutionRecord>();

    [JsonIgnore]
    public int RunningCount => Items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Running, StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public int PendingCount => Items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public int CompletedCount => Items.Count(item =>
        string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Succeeded, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase));

    [JsonIgnore]
    public double ProgressRatio => TotalCount <= 0 ? 0d : (double)CompletedCount / TotalCount;

    [JsonIgnore]
    public string ProgressText => $"{CompletedCount}/{TotalCount}";

    [JsonIgnore]
    public string StatusText => AiInspectionTaskTextMapper.ToBatchStatusText(Status);

    [JsonIgnore]
    public string TaskTypeText => AiInspectionTaskTextMapper.ToTaskTypeText(TaskType);

    [JsonIgnore]
    public string ScopeModeText => AiInspectionTaskTextMapper.ToScopeModeText(ScopeMode);

    [JsonIgnore]
    public string AccentResourceKey => AiInspectionTaskTextMapper.ToAccentResourceKey(Status);

    [JsonIgnore]
    public string SourceText => AiInspectionTaskTextMapper.ToBatchSourceText(SourceKind, SourcePlanName);

    [JsonIgnore]
    public bool IsPlanTriggered => string.Equals(SourceKind, AiInspectionTaskBatchSourceKind.Plan, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool CanStart =>
        string.Equals(Status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool CanCancel =>
        string.Equals(Status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, AiInspectionTaskStatus.Running, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase);
}
