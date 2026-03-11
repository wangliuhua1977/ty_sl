using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class AiInspectionTaskPlan
{
    public string PlanId { get; init; } = Guid.NewGuid().ToString("N");

    public string PlanName { get; init; } = string.Empty;

    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public string TaskType { get; init; } = AiInspectionTaskType.BasicInspection;

    public string ScopeMode { get; init; } = AiInspectionTaskScopeMode.FullScheme;

    public string ScheduleType { get; init; } = AiInspectionTaskPlanScheduleType.Daily;

    public int DailyHour { get; init; }

    public int DailyMinute { get; init; }

    public DateTimeOffset NextRunAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset? LastRunAt { get; init; }

    public bool IsEnabled { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

    public string CreatedBy { get; init; } = string.Empty;

    public string LastTriggeredTaskId { get; init; } = string.Empty;

    [JsonIgnore]
    public string TaskTypeText => AiInspectionTaskTextMapper.ToTaskTypeText(TaskType);

    [JsonIgnore]
    public string ScopeModeText => AiInspectionTaskTextMapper.ToScopeModeText(ScopeMode);

    [JsonIgnore]
    public string ScheduleText => AiInspectionTaskTextMapper.ToPlanScheduleText(ScheduleType, DailyHour, DailyMinute);

    [JsonIgnore]
    public string EnabledText => AiInspectionTaskTextMapper.ToPlanEnabledText(IsEnabled);

    [JsonIgnore]
    public string NextRunAtText => NextRunAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string LastRunAtText => LastRunAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
}
