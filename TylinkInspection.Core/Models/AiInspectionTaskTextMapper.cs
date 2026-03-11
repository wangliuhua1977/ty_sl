namespace TylinkInspection.Core.Models;

public static class AiInspectionTaskTextMapper
{
    public static string ToBatchStatusText(string? status)
    {
        return status switch
        {
            AiInspectionTaskStatus.Pending => "待执行",
            AiInspectionTaskStatus.Running => "执行中",
            AiInspectionTaskStatus.Succeeded => "已完成",
            AiInspectionTaskStatus.Failed => "失败",
            AiInspectionTaskStatus.PartiallyCompleted => "部分完成",
            AiInspectionTaskStatus.Canceled => "已取消",
            _ => "未知"
        };
    }

    public static string ToItemStatusText(string? status)
    {
        return status switch
        {
            AiInspectionTaskItemStatus.Pending => "待执行",
            AiInspectionTaskItemStatus.Running => "执行中",
            AiInspectionTaskItemStatus.Succeeded => "成功",
            AiInspectionTaskItemStatus.Failed => "失败",
            AiInspectionTaskItemStatus.Canceled => "已取消",
            _ => "未知"
        };
    }

    public static string ToTaskTypeText(string? taskType)
    {
        return taskType switch
        {
            AiInspectionTaskType.BasicInspection => "基础巡检",
            AiInspectionTaskType.PlaybackReview => "播放复核",
            AiInspectionTaskType.ScreenshotReviewPreparation => "截图复核预备",
            AiInspectionTaskType.Recheck => "复检任务",
            _ => "未知类型"
        };
    }

    public static string ToScopeModeText(string? scopeMode)
    {
        return scopeMode switch
        {
            AiInspectionTaskScopeMode.FullScheme => "当前方案全量",
            AiInspectionTaskScopeMode.AbnormalOnly => "仅异常点位",
            AiInspectionTaskScopeMode.FocusedOnly => "仅重点关注",
            AiInspectionTaskScopeMode.PendingRecheckOnly => "仅待复检",
            _ => "未知范围"
        };
    }

    public static string ToPlanScheduleText(string? scheduleType, int dailyHour, int dailyMinute)
    {
        return scheduleType switch
        {
            AiInspectionTaskPlanScheduleType.Daily => $"每日 {dailyHour:00}:{dailyMinute:00}",
            _ => "未知计划"
        };
    }

    public static string ToPlanEnabledText(bool isEnabled)
    {
        return isEnabled ? "已启用" : "已停用";
    }

    public static string ToBatchSourceText(string? sourceKind, string? planName)
    {
        return sourceKind switch
        {
            AiInspectionTaskBatchSourceKind.Plan when !string.IsNullOrWhiteSpace(planName) => $"计划 / {planName.Trim()}",
            AiInspectionTaskBatchSourceKind.Plan => "计划实例化",
            AiInspectionTaskBatchSourceKind.Retry => "子任务重试",
            AiInspectionTaskBatchSourceKind.Rerun => "批次重跑",
            _ => "手工创建"
        };
    }

    public static string ToAccentResourceKey(string? status)
    {
        return status switch
        {
            AiInspectionTaskStatus.Pending => "ToneWarningBrush",
            AiInspectionTaskStatus.Running => "TonePrimaryBrush",
            AiInspectionTaskStatus.Succeeded => "ToneSuccessBrush",
            AiInspectionTaskStatus.Failed => "ToneDangerBrush",
            AiInspectionTaskStatus.PartiallyCompleted => "ToneFocusBrush",
            AiInspectionTaskStatus.Canceled => "ToneInfoBrush",
            _ => "TonePrimaryBrush"
        };
    }

    public static string ToItemAccentResourceKey(string? status, bool isAbnormal)
    {
        if (string.Equals(status, AiInspectionTaskItemStatus.Succeeded, StringComparison.OrdinalIgnoreCase))
        {
            return isAbnormal ? "ToneWarningBrush" : "ToneSuccessBrush";
        }

        return status switch
        {
            AiInspectionTaskItemStatus.Pending => "ToneWarningBrush",
            AiInspectionTaskItemStatus.Running => "TonePrimaryBrush",
            AiInspectionTaskItemStatus.Failed => "ToneDangerBrush",
            AiInspectionTaskItemStatus.Canceled => "ToneInfoBrush",
            _ => "TonePrimaryBrush"
        };
    }
}
