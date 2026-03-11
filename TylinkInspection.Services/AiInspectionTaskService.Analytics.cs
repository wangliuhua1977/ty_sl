using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed partial class AiInspectionTaskService
{
    public IReadOnlyList<AiInspectionTaskPlanExecutionHistory> GetPlanExecutionHistory()
    {
        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            return BuildPlanExecutionHistoryNoLock();
        }
    }

    public IReadOnlyList<AiInspectionTaskBatch> GetPlanExecutionBatches(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);

        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            return _batches
                .Where(item => string.Equals(item.SourcePlanId, planId.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreatedAt)
                .ToList();
        }
    }

    public AiInspectionFailureDashboard GetFailureDashboard()
    {
        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            return BuildFailureDashboardNoLock();
        }
    }

    public AiInspectionTaskContextSummary? GetTaskContext(InspectionModuleNavigationContext? context)
    {
        if (context is null)
        {
            return null;
        }

        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            return ResolveTaskContextNoLock(context);
        }
    }

    private IReadOnlyList<AiInspectionTaskPlanExecutionHistory> BuildPlanExecutionHistoryNoLock()
    {
        var batchesByPlanId = _batches
            .Where(item => !string.IsNullOrWhiteSpace(item.SourcePlanId))
            .GroupBy(item => item.SourcePlanId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.CreatedAt).ToList(),
                StringComparer.OrdinalIgnoreCase);

        return _plans
            .Select(plan =>
            {
                var planBatches = batchesByPlanId.GetValueOrDefault(plan.PlanId) ?? [];
                var latestBatch = planBatches.FirstOrDefault();
                return new AiInspectionTaskPlanExecutionHistory
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    TaskType = plan.TaskType,
                    TaskTypeText = plan.TaskTypeText,
                    ScopeMode = plan.ScopeMode,
                    ScopeModeText = plan.ScopeModeText,
                    ScheduleType = plan.ScheduleType,
                    ScheduleText = plan.ScheduleText,
                    IsEnabled = plan.IsEnabled,
                    LastRunAt = latestBatch?.StartedAt ?? latestBatch?.CreatedAt ?? plan.LastRunAt,
                    LatestTaskId = latestBatch?.TaskId ?? plan.LastTriggeredTaskId,
                    LatestTaskName = latestBatch?.TaskName ?? string.Empty,
                    LatestTaskStatus = latestBatch?.Status ?? string.Empty,
                    LatestTaskStatusText = latestBatch?.StatusText ?? "--",
                    LatestResultSummary = latestBatch?.LatestResultSummary ?? "计划暂未实例化批次。",
                    SuccessCount = latestBatch?.ResultSummary.SuccessCount ?? 0,
                    FailedCount = latestBatch?.ResultSummary.FailedCount ?? 0,
                    AbnormalCount = latestBatch?.ResultSummary.AbnormalCount ?? 0,
                    PendingManualReviewCount = latestBatch?.ResultSummary.PendingManualReviewCount ?? 0,
                    PendingClosureCount = latestBatch?.ResultSummary.PendingClosureCount ?? 0,
                    ExecutedBatchCount = planBatches.Count,
                    FailedBatchCount = planBatches.Count(IsFailedBatch)
                };
            })
            .OrderByDescending(item => item.FailedBatchCount)
            .ThenByDescending(item => item.LastRunAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.PlanName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private AiInspectionFailureDashboard BuildFailureDashboardNoLock()
    {
        var failedBatches = _batches
            .Where(IsFailedBatch)
            .OrderByDescending(item => item.CompletedAt ?? item.StartedAt ?? item.CreatedAt)
            .ToList();

        var failedAttempts = _batches
            .SelectMany(batch => batch.Items
                .Where(IsFailedItem)
                .Select(item => new FailedAttempt(batch, item, ResolveFailureReason(batch, item), ResolveFailureCategory(batch, item))))
            .OrderByDescending(item => item.OccurredAt)
            .ToList();

        var failedPlans = _plans
            .Select(plan =>
            {
                var planFailures = failedBatches
                    .Where(item => string.Equals(item.SourcePlanId, plan.PlanId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (planFailures.Count == 0)
                {
                    return null;
                }

                var latestFailure = planFailures
                    .OrderByDescending(item => item.CompletedAt ?? item.StartedAt ?? item.CreatedAt)
                    .First();
                return new AiInspectionFailedPlanSummary
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    TaskType = plan.TaskType,
                    TaskTypeText = plan.TaskTypeText,
                    IsEnabled = plan.IsEnabled,
                    FailedBatchCount = planFailures.Count,
                    FailedPointCount = planFailures.Sum(item => item.FailedCount),
                    LastFailedAt = latestFailure.CompletedAt ?? latestFailure.StartedAt ?? latestFailure.CreatedAt,
                    LatestFailureSummary = FirstNonEmpty(
                        latestFailure.FailureSummary,
                        latestFailure.LatestResultSummary,
                        "最近一次执行失败。")
                };
            })
            .Where(item => item is not null)
            .Cast<AiInspectionFailedPlanSummary>()
            .OrderByDescending(item => item.FailedBatchCount)
            .ThenByDescending(item => item.LastFailedAt ?? DateTimeOffset.MinValue)
            .Take(6)
            .ToList();

        var failedBatchSummaries = failedBatches
            .Take(8)
            .Select(batch => new AiInspectionFailedBatchSummary
            {
                TaskId = batch.TaskId,
                TaskName = batch.TaskName,
                TaskType = batch.TaskType,
                TaskTypeText = batch.TaskTypeText,
                SourceText = batch.SourceText,
                StatusText = batch.StatusText,
                FailedCount = batch.FailedCount,
                AbnormalCount = batch.AbnormalCount,
                OccurredAt = batch.CompletedAt ?? batch.StartedAt ?? batch.CreatedAt,
                FailureSummary = FirstNonEmpty(
                    batch.FailureSummary,
                    batch.LatestResultSummary,
                    "批次存在失败项。")
            })
            .ToList();

        var failedPointSummaries = failedAttempts
            .Take(10)
            .Select(item => new AiInspectionFailedPointSummary
            {
                DeviceCode = item.Item.DeviceCode,
                DeviceName = item.Item.DeviceName,
                TaskId = item.Batch.TaskId,
                TaskName = item.Batch.TaskName,
                TaskItemId = item.Item.ItemId,
                TaskType = item.Batch.TaskType,
                TaskTypeText = item.Batch.TaskTypeText,
                AttemptCount = item.Item.AttemptCount,
                OccurredAt = item.OccurredAt,
                FailureReason = item.FailureReason,
                ResultSummary = FirstNonEmpty(item.Item.LastResultSummary, item.Batch.LatestResultSummary, "--")
            })
            .ToList();

        var failureReasons = failedAttempts
            .GroupBy(item => item.FailureCategory, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AiInspectionFailureReasonStat
            {
                CategoryName = group.Key,
                FailedItemCount = group.Count(),
                AffectedPointCount = group
                    .Select(item => item.Item.DeviceCode)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                LastOccurredAt = group.Max(item => item.OccurredAt)
            })
            .OrderByDescending(item => item.FailedItemCount)
            .ThenByDescending(item => item.LastOccurredAt ?? DateTimeOffset.MinValue)
            .Take(8)
            .ToList();

        var taskTypeFailures = failedBatches
            .GroupBy(item => item.TaskType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AiInspectionTaskTypeFailureStat
            {
                TaskType = group.Key,
                TaskTypeText = group.First().TaskTypeText,
                FailedBatchCount = group.Count(),
                FailedItemCount = group.Sum(item => item.FailedCount)
            })
            .OrderByDescending(item => item.FailedItemCount)
            .ThenByDescending(item => item.FailedBatchCount)
            .Take(6)
            .ToList();

        var repeatedFailurePoints = BuildRepeatedFailurePointSummaries();

        return new AiInspectionFailureDashboard
        {
            FailedPlans = failedPlans,
            FailedBatches = failedBatchSummaries,
            FailedPoints = failedPointSummaries,
            FailureReasons = failureReasons,
            RepeatedFailurePoints = repeatedFailurePoints,
            TaskTypeFailures = taskTypeFailures
        };
    }

    private IReadOnlyList<AiInspectionContinuousFailurePointSummary> BuildRepeatedFailurePointSummaries()
    {
        var allAttempts = _batches
            .SelectMany(batch => batch.Items.Select(item => new AttemptRecord(
                batch,
                item,
                item.CompletedAt ?? item.StartedAt ?? batch.CompletedAt ?? batch.StartedAt ?? batch.CreatedAt,
                ResolveFailureReason(batch, item))))
            .OrderBy(item => item.OccurredAt)
            .ToList();

        return allAttempts
            .Where(item => !string.IsNullOrWhiteSpace(item.Item.DeviceCode))
            .GroupBy(item => item.Item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var failedTaskCount = 0;
                var consecutiveFailureCount = 0;
                var currentConsecutive = 0;
                DateTimeOffset? lastFailedAt = null;
                var latestFailureReason = string.Empty;
                var deviceName = group.LastOrDefault(item => !string.IsNullOrWhiteSpace(item.Item.DeviceName))?.Item.DeviceName ?? group.Key;

                foreach (var attempt in group.OrderBy(item => item.OccurredAt))
                {
                    if (IsFailedItem(attempt.Item))
                    {
                        failedTaskCount++;
                        currentConsecutive++;
                        consecutiveFailureCount = Math.Max(consecutiveFailureCount, currentConsecutive);
                        lastFailedAt = attempt.OccurredAt;
                        latestFailureReason = attempt.FailureReason;
                        continue;
                    }

                    currentConsecutive = 0;
                }

                return new AiInspectionContinuousFailurePointSummary
                {
                    DeviceCode = group.Key,
                    DeviceName = deviceName,
                    FailedTaskCount = failedTaskCount,
                    ConsecutiveFailureCount = consecutiveFailureCount,
                    LastFailedAt = lastFailedAt,
                    LatestFailureReason = FirstNonEmpty(latestFailureReason, "重复失败")
                };
            })
            .Where(item => item.FailedTaskCount >= 2)
            .OrderByDescending(item => item.ConsecutiveFailureCount)
            .ThenByDescending(item => item.FailedTaskCount)
            .ThenByDescending(item => item.LastFailedAt ?? DateTimeOffset.MinValue)
            .Take(8)
            .ToList();
    }

    private AiInspectionTaskContextSummary? ResolveTaskContextNoLock(InspectionModuleNavigationContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.TaskId))
        {
            var batch = _batches.FirstOrDefault(item => string.Equals(item.TaskId, context.TaskId, StringComparison.OrdinalIgnoreCase));
            if (batch is not null)
            {
                var item = ResolveContextItem(batch, context, allowFallback: true);
                return BuildTaskContextSummary(batch, item);
            }
        }

        foreach (var batch in _batches.OrderByDescending(item => item.CreatedAt))
        {
            var item = ResolveContextItem(batch, context, allowFallback: false);
            if (item is not null)
            {
                return BuildTaskContextSummary(batch, item);
            }
        }

        return null;
    }

    private static AiInspectionTaskContextSummary BuildTaskContextSummary(AiInspectionTaskBatch batch, AiInspectionTaskItem? item)
    {
        var executedAt = item?.CompletedAt
            ?? item?.StartedAt
            ?? batch.CompletedAt
            ?? batch.StartedAt
            ?? batch.CreatedAt;

        return new AiInspectionTaskContextSummary
        {
            TaskId = batch.TaskId,
            TaskName = batch.TaskName,
            SchemeId = batch.SchemeId,
            SchemeName = batch.SchemeName,
            PlanId = batch.SourcePlanId,
            PlanName = batch.SourcePlanName,
            TaskType = batch.TaskType,
            TaskTypeText = batch.TaskTypeText,
            SourceText = batch.SourceText,
            TaskStatus = batch.Status,
            TaskStatusText = batch.StatusText,
            TaskItemId = item?.ItemId ?? string.Empty,
            DeviceCode = item?.DeviceCode ?? string.Empty,
            DeviceName = item?.DeviceName ?? string.Empty,
            ItemStatus = item?.ExecutionStatus ?? string.Empty,
            ItemStatusText = item?.ExecutionStatusText ?? "--",
            EvidenceId = FirstNonEmpty(item?.LinkedScreenshotSampleId, item?.LinkedReviewId),
            ClosureId = FirstNonEmpty(item?.LinkedClosureId),
            IsAbnormalResult = item?.IsAbnormalResult == true,
            ExecutedAt = executedAt,
            TaskStartedAt = batch.StartedAt ?? batch.CreatedAt,
            TaskCompletedAt = batch.CompletedAt,
            ResultSummary = FirstNonEmpty(item?.LastResultSummary, batch.LatestResultSummary, "--"),
            FailureSummary = FirstNonEmpty(item?.LastError, batch.FailureSummary)
        };
    }

    private static AiInspectionTaskItem? ResolveContextItem(
        AiInspectionTaskBatch batch,
        InspectionModuleNavigationContext context,
        bool allowFallback)
    {
        if (!string.IsNullOrWhiteSpace(context.TaskItemId))
        {
            var matchedByItemId = batch.Items.FirstOrDefault(item =>
                string.Equals(item.ItemId, context.TaskItemId, StringComparison.OrdinalIgnoreCase));
            if (matchedByItemId is not null)
            {
                return matchedByItemId;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.DeviceCode))
        {
            var matchedByDevice = batch.Items.FirstOrDefault(item =>
                string.Equals(item.DeviceCode, context.DeviceCode, StringComparison.OrdinalIgnoreCase));
            if (matchedByDevice is not null)
            {
                return matchedByDevice;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.EvidenceId))
        {
            var matchedByEvidence = batch.Items.FirstOrDefault(item =>
                string.Equals(item.LinkedScreenshotSampleId, context.EvidenceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.LinkedReviewId, context.EvidenceId, StringComparison.OrdinalIgnoreCase));
            if (matchedByEvidence is not null)
            {
                return matchedByEvidence;
            }
        }

        if (!string.IsNullOrWhiteSpace(context.ClosureId))
        {
            var matchedByClosure = batch.Items.FirstOrDefault(item =>
                string.Equals(item.LinkedClosureId, context.ClosureId, StringComparison.OrdinalIgnoreCase));
            if (matchedByClosure is not null)
            {
                return matchedByClosure;
            }
        }

        return allowFallback ? batch.Items.FirstOrDefault() : null;
    }

    private static bool IsFailedBatch(AiInspectionTaskBatch batch)
    {
        return batch.FailedCount > 0 ||
               string.Equals(batch.Status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(batch.Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailedItem(AiInspectionTaskItem item)
    {
        return string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFailureReason(AiInspectionTaskBatch batch, AiInspectionTaskItem item)
    {
        return FirstNonEmpty(
            NormalizeSingleLine(item.LastError),
            NormalizeSingleLine(item.LastResultSummary),
            NormalizeSingleLine(batch.FailureSummary),
            NormalizeSingleLine(batch.LatestResultSummary),
            "任务执行失败");
    }

    private static string ResolveFailureCategory(AiInspectionTaskBatch batch, AiInspectionTaskItem item)
    {
        var reason = ResolveFailureReason(batch, item);
        if (reason.Contains("超时", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("失效", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("令牌", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            return "连接/令牌异常";
        }

        if (string.Equals(batch.TaskType, AiInspectionTaskType.ScreenshotReviewPreparation, StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("截图", StringComparison.OrdinalIgnoreCase))
        {
            return "截图预备失败";
        }

        if (string.Equals(batch.TaskType, AiInspectionTaskType.PlaybackReview, StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("播放", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("回看", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("流", StringComparison.OrdinalIgnoreCase))
        {
            return "播放复核失败";
        }

        if (string.Equals(batch.TaskType, AiInspectionTaskType.Recheck, StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("复检", StringComparison.OrdinalIgnoreCase))
        {
            return "复检失败";
        }

        if (reason.Contains("闭环", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("工单", StringComparison.OrdinalIgnoreCase))
        {
            return "闭环联动失败";
        }

        if (string.Equals(batch.TaskType, AiInspectionTaskType.BasicInspection, StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("巡检", StringComparison.OrdinalIgnoreCase))
        {
            return "基础巡检失败";
        }

        return "其他失败";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private sealed record class FailedAttempt(
        AiInspectionTaskBatch Batch,
        AiInspectionTaskItem Item,
        string FailureReason,
        string FailureCategory)
    {
        public DateTimeOffset OccurredAt => Item.CompletedAt ?? Item.StartedAt ?? Batch.CompletedAt ?? Batch.StartedAt ?? Batch.CreatedAt;
    }

    private sealed record class AttemptRecord(
        AiInspectionTaskBatch Batch,
        AiInspectionTaskItem Item,
        DateTimeOffset OccurredAt,
        string FailureReason);
}
