namespace TylinkInspection.Core.Models;

public static class RecheckTextMapper
{
    public static string ToTaskStatusText(string? status)
    {
        return status switch
        {
            RecheckTaskStatuses.Pending => "待执行",
            RecheckTaskStatuses.Scheduled => "已调度",
            RecheckTaskStatuses.Running => "执行中",
            RecheckTaskStatuses.Succeeded => "执行成功",
            RecheckTaskStatuses.Failed => "执行失败",
            RecheckTaskStatuses.Canceled => "已取消",
            RecheckTaskStatuses.Completed => "已完成",
            _ => "未知状态"
        };
    }

    public static string ToScheduleTypeText(string? scheduleType)
    {
        return scheduleType switch
        {
            RecheckScheduleTypes.FixedDelay => "固定间隔",
            RecheckScheduleTypes.SpecificTime => "指定时间",
            RecheckScheduleTypes.ManualOnly => "仅手动",
            _ => "未定义"
        };
    }

    public static string ToExecutionOutcomeText(string? outcome)
    {
        return outcome switch
        {
            RecheckExecutionOutcomes.Passed => "复检通过",
            RecheckExecutionOutcomes.Failed => "继续待复检",
            RecheckExecutionOutcomes.Error => "执行异常",
            RecheckExecutionOutcomes.Canceled => "任务取消",
            RecheckExecutionOutcomes.Completed => "任务完成",
            _ => "未执行"
        };
    }

    public static string ToTriggerTypeText(string? triggerType)
    {
        return triggerType switch
        {
            RecheckExecutionTriggerTypes.Manual => "手动触发",
            RecheckExecutionTriggerTypes.Retry => "失败重试",
            RecheckExecutionTriggerTypes.Recovery => "启动恢复",
            _ => "自动调度"
        };
    }

    public static string ToRuleScopeText(string? scopeType)
    {
        return scopeType switch
        {
            RecheckRuleScopeTypes.FaultType => "故障类型规则",
            RecheckRuleScopeTypes.ManualOverride => "单任务覆盖",
            _ => "全局默认规则"
        };
    }

    public static string ToRuleHitSourceText(string? sourceType)
    {
        return sourceType switch
        {
            RecheckRuleHitSources.ManualOverride => "单任务覆盖规则",
            RecheckRuleHitSources.FaultTypeRule => "故障类型规则",
            _ => "全局默认规则"
        };
    }
}
