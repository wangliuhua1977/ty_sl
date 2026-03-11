namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskCreateRequest
{
    public string TaskName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string TaskType { get; init; } = AiInspectionTaskType.BasicInspection;

    public string ScopeMode { get; init; } = AiInspectionTaskScopeMode.FullScheme;

    public string SourceKind { get; init; } = AiInspectionTaskBatchSourceKind.Manual;

    public string SourcePlanId { get; init; } = string.Empty;

    public string SourcePlanName { get; init; } = string.Empty;

    public string ParentTaskId { get; init; } = string.Empty;

    public string ParentTaskName { get; init; } = string.Empty;

    public string CreatedBy { get; init; } = string.Empty;

    public bool ExecuteImmediately { get; init; } = true;
}
