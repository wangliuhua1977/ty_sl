namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskPlanCreateRequest
{
    public string PlanName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string TaskType { get; init; } = AiInspectionTaskType.BasicInspection;

    public string ScopeMode { get; init; } = AiInspectionTaskScopeMode.FullScheme;

    public string ScheduleType { get; init; } = AiInspectionTaskPlanScheduleType.Daily;

    public int DailyHour { get; init; }

    public int DailyMinute { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string CreatedBy { get; init; } = string.Empty;
}
