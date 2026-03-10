using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class AiInspectionCenterService : IAiInspectionCenterService
{
    private readonly IAiInspectionTaskStore _taskStore;

    public AiInspectionCenterService(IAiInspectionTaskStore taskStore)
    {
        _taskStore = taskStore;
        EnsureSeedData();
    }

    public IReadOnlyList<AiInspectionTaskListItem> Query(AiInspectionTaskQuery query)
    {
        var filtered = ApplyFilter(_taskStore.LoadAll(), query);
        return filtered
            .OrderByDescending(task => task.ScheduledAt)
            .Select(MapToListItem)
            .ToList();
    }

    public AiInspectionTaskDetail? GetDetail(string taskId)
    {
        return _taskStore.LoadAll().FirstOrDefault(task => task.TaskId == taskId);
    }

    public void UpdateStatus(AiInspectionTaskMutation mutation)
    {
        var tasks = _taskStore.LoadAll().ToList();
        var task = tasks.FirstOrDefault(item => item.TaskId == mutation.TaskId);
        if (task is null)
        {
            return;
        }

        var updated = new AiInspectionTaskDetail
        {
            TaskId = task.TaskId,
            Title = task.Title,
            DeviceCode = task.DeviceCode,
            RegionName = task.RegionName,
            Status = mutation.TargetStatus,
            SourceName = task.SourceName,
            StrategyName = task.StrategyName,
            Description = task.Description,
            ScheduledAt = task.ScheduledAt,
            StartedAt = mutation.TargetStatus == AiInspectionTaskStatus.Pending ? null : mutation.TargetStatus == AiInspectionTaskStatus.Running ? DateTimeOffset.Now : task.StartedAt,
            FinishedAt = mutation.TargetStatus == AiInspectionTaskStatus.Completed || mutation.TargetStatus == AiInspectionTaskStatus.Faulted ? DateTimeOffset.Now : null,
            LatestNote = mutation.Note ?? task.LatestNote,
            ExecutionRecords = task.ExecutionRecords
                .Append(new AiInspectionExecutionRecord
                {
                    RecordId = Guid.NewGuid().ToString("N"),
                    Timestamp = DateTimeOffset.Now,
                    Message = $"任务状态更新为 {mutation.TargetStatus}{(string.IsNullOrWhiteSpace(mutation.Note) ? string.Empty : $"，备注：{mutation.Note}")}",
                    AccentResourceKey = MapAccent(mutation.TargetStatus)
                })
                .OrderByDescending(record => record.Timestamp)
                .ToList()
        };

        ReplaceAndSave(tasks, updated);
    }

    public void RetryTask(string taskId, string? note)
    {
        UpdateStatus(new AiInspectionTaskMutation
        {
            TaskId = taskId,
            TargetStatus = AiInspectionTaskStatus.Running,
            Note = string.IsNullOrWhiteSpace(note) ? "手动重试任务" : note
        });
    }

    private void EnsureSeedData()
    {
        if (_taskStore.LoadAll().Count > 0)
        {
            return;
        }

        _taskStore.SaveAll(
        [
            BuildTask("AI-INS-1001", "华东晨检批次", "TY-SH-0001", "上海 / 虹桥", AiInspectionTaskStatus.Pending, "AI规则补扫", "跨区域晨检模板", "待执行常规视频流抽帧与 AI 异常态检测。", DateTimeOffset.Now.AddMinutes(20), null, null, null),
            BuildTask("AI-INS-1002", "交通枢纽专项巡检", "TY-HZ-0008", "杭州 / 西站", AiInspectionTaskStatus.Running, "AI专项任务", "高优点位巡检策略", "正在执行画面异常、离线与遮挡联合分析。", DateTimeOffset.Now.AddMinutes(-35), DateTimeOffset.Now.AddMinutes(-22), null, "运行中"),
            BuildTask("AI-INS-1003", "园区边缘点位补扫", "TY-SZ-0017", "苏州 / 园区", AiInspectionTaskStatus.Faulted, "异常重试队列", "边缘点位补扫模板", "上次执行过程中出现接口超时，需人工复核后重试。", DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-2).AddMinutes(6), null, "接口超时"),
            BuildTask("AI-INS-1004", "重点关注点位复检", "TY-NB-0012", "宁波 / 港区", AiInspectionTaskStatus.Completed, "人工触发复检", "重点点位复核策略", "已完成重点点位复检，暂无新增异常。", DateTimeOffset.Now.AddHours(-3), DateTimeOffset.Now.AddHours(-3).AddMinutes(10), DateTimeOffset.Now.AddHours(-2).AddMinutes(18), "完成"),
            BuildTask("AI-INS-1005", "片区午检批次", "TY-HF-0022", "合肥 / 南广场", AiInspectionTaskStatus.Pending, "定时批处理", "午间巡检模板", "待执行午间轮询。", DateTimeOffset.Now.AddMinutes(45), null, null, null)
        ]);
    }

    private static IEnumerable<AiInspectionTaskDetail> ApplyFilter(IReadOnlyList<AiInspectionTaskDetail> tasks, AiInspectionTaskQuery query)
    {
        var filtered = tasks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            filtered = filtered.Where(task =>
                task.Title.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                task.RegionName.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.DeviceCode))
        {
            filtered = filtered.Where(task => task.DeviceCode.Contains(query.DeviceCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(task => task.Status == query.Status);
        }

        if (query.StartTime is not null)
        {
            filtered = filtered.Where(task => task.ScheduledAt >= query.StartTime.Value);
        }

        if (query.EndTime is not null)
        {
            filtered = filtered.Where(task => task.ScheduledAt <= query.EndTime.Value);
        }

        return filtered;
    }

    private void ReplaceAndSave(List<AiInspectionTaskDetail> tasks, AiInspectionTaskDetail updated)
    {
        var index = tasks.FindIndex(item => item.TaskId == updated.TaskId);
        if (index >= 0)
        {
            tasks[index] = updated;
            _taskStore.SaveAll(tasks);
        }
    }

    private static AiInspectionTaskListItem MapToListItem(AiInspectionTaskDetail task)
    {
        return new AiInspectionTaskListItem
        {
            TaskId = task.TaskId,
            Title = task.Title,
            DeviceCode = task.DeviceCode,
            RegionName = task.RegionName,
            Status = task.Status,
            SourceName = task.SourceName,
            ScheduledAt = task.ScheduledAt,
            StartedAt = task.StartedAt,
            FinishedAt = task.FinishedAt,
            AccentResourceKey = MapAccent(task.Status)
        };
    }

    private static AiInspectionTaskDetail BuildTask(
        string taskId,
        string title,
        string deviceCode,
        string region,
        string status,
        string sourceName,
        string strategyName,
        string description,
        DateTimeOffset scheduledAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        string? latestNote)
    {
        return new AiInspectionTaskDetail
        {
            TaskId = taskId,
            Title = title,
            DeviceCode = deviceCode,
            RegionName = region,
            Status = status,
            SourceName = sourceName,
            StrategyName = strategyName,
            Description = description,
            ScheduledAt = scheduledAt,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            LatestNote = latestNote,
            ExecutionRecords =
            [
                new AiInspectionExecutionRecord
                {
                    RecordId = Guid.NewGuid().ToString("N"),
                    Timestamp = scheduledAt,
                    Message = $"任务已创建，当前状态：{status}",
                    AccentResourceKey = MapAccent(status)
                }
            ]
        };
    }

    private static string MapAccent(string status)
    {
        return status switch
        {
            AiInspectionTaskStatus.Pending => "TonePrimaryBrush",
            AiInspectionTaskStatus.Running => "ToneInfoBrush",
            AiInspectionTaskStatus.Completed => "ToneSuccessBrush",
            AiInspectionTaskStatus.Faulted => "ToneDangerBrush",
            _ => "TonePrimaryBrush"
        };
    }
}
