using System.Windows.Input;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class AiInspectionCenterPageViewModel
{
    private string _activeFailureReasonFilter = string.Empty;
    private string _quickTaskTypeFilter = string.Empty;
    private string _focusedTaskId = string.Empty;
    private string _focusedTaskItemId = string.Empty;
    private string _focusedDeviceCode = string.Empty;
    private string _focusedEvidenceId = string.Empty;
    private string _focusedClosureId = string.Empty;
    private string _taskCenterContextText = "历史记录、失败看板和目标页都可以直接回流到当前任务批次。";

    private ICommand? _selectTaskBatchCommand;
    private ICommand? _openHistoryLatestBatchCommand;
    private ICommand? _navigateHistoryBatchToMapCommand;
    private ICommand? _navigateHistoryBatchToPointGovernanceCommand;
    private ICommand? _navigateHistoryBatchToReviewCommand;
    private ICommand? _navigateHistoryBatchToFaultClosureCommand;
    private ICommand? _openFailedPlanCommand;
    private ICommand? _openFailedBatchCommand;
    private ICommand? _openFailedPointCommand;
    private ICommand? _applyFailureReasonFilterCommand;
    private ICommand? _applyTaskTypeFilterCommand;
    private ICommand? _openRepeatedFailurePointCommand;

    public string TaskCenterContextText
    {
        get => _taskCenterContextText;
        private set => SetProperty(ref _taskCenterContextText, value);
    }

    public string TaskCenterFilterText
    {
        get
        {
            var filters = new List<string>();
            if (!string.IsNullOrWhiteSpace(_quickTaskTypeFilter))
            {
                filters.Add($"任务类型：{AiInspectionTaskTextMapper.ToTaskTypeText(_quickTaskTypeFilter)}");
            }

            if (!string.IsNullOrWhiteSpace(_activeFailureReasonFilter))
            {
                filters.Add($"失败原因：{_activeFailureReasonFilter}");
            }

            return filters.Count == 0
                ? "当前显示全部批次，可从计划历史、失败看板和目标页上下文直接聚焦任务。"
                : $"当前筛选：{string.Join(" / ", filters)}";
        }
    }

    public string SelectedTaskFailureDigestText => SelectedTask is null
        ? "--"
        : FirstNonEmpty(SelectedTask.FailureSummary, SelectedTask.LatestResultSummary, "当前批次暂无失败或异常摘要。");

    public string SelectedTaskJumpEntryText => SelectedTask is null
        ? "请先选择任务批次。"
        : "地图、点位治理、复核中心、故障闭环入口均已绑定当前批次上下文。";

    public ICommand SelectTaskBatchCommand => _selectTaskBatchCommand ??=
        new RelayCommand<AiInspectionTaskBatch>(batch => OpenBatchDetail(batch, updateContextText: false));

    public ICommand OpenHistoryLatestBatchCommand => _openHistoryLatestBatchCommand ??=
        new RelayCommand<AiInspectionTaskPlanExecutionHistory>(OpenHistoryLatestBatch);

    public ICommand NavigateHistoryBatchToMapCommand => _navigateHistoryBatchToMapCommand ??=
        new RelayCommand<AiInspectionTaskBatch>(batch => NavigateBatchToPage(batch, InspectionModulePageKeys.MapInspection, ResolvePointNavigationItem));

    public ICommand NavigateHistoryBatchToPointGovernanceCommand => _navigateHistoryBatchToPointGovernanceCommand ??=
        new RelayCommand<AiInspectionTaskBatch>(batch => NavigateBatchToPage(batch, InspectionModulePageKeys.PointGovernance, ResolvePointNavigationItem));

    public ICommand NavigateHistoryBatchToReviewCommand => _navigateHistoryBatchToReviewCommand ??=
        new RelayCommand<AiInspectionTaskBatch>(batch => NavigateBatchToPage(batch, InspectionModulePageKeys.ReviewCenter, ResolveReviewNavigationItem));

    public ICommand NavigateHistoryBatchToFaultClosureCommand => _navigateHistoryBatchToFaultClosureCommand ??=
        new RelayCommand<AiInspectionTaskBatch>(batch => NavigateBatchToPage(batch, InspectionModulePageKeys.FaultClosure, ResolveClosureNavigationItem));

    public ICommand OpenFailedPlanCommand => _openFailedPlanCommand ??=
        new RelayCommand<AiInspectionFailedPlanSummary>(OpenFailedPlan);

    public ICommand OpenFailedBatchCommand => _openFailedBatchCommand ??=
        new RelayCommand<AiInspectionFailedBatchSummary>(OpenFailedBatch);

    public ICommand OpenFailedPointCommand => _openFailedPointCommand ??=
        new RelayCommand<AiInspectionFailedPointSummary>(OpenFailedPoint);

    public ICommand ApplyFailureReasonFilterCommand => _applyFailureReasonFilterCommand ??=
        new RelayCommand<AiInspectionFailureReasonStat>(ApplyFailureReasonFilter);

    public ICommand ApplyTaskTypeFilterCommand => _applyTaskTypeFilterCommand ??=
        new RelayCommand<AiInspectionTaskTypeFailureStat>(ApplyTaskTypeFilter);

    public ICommand OpenRepeatedFailurePointCommand => _openRepeatedFailurePointCommand ??=
        new RelayCommand<AiInspectionContinuousFailurePointSummary>(OpenRepeatedFailurePoint);

    private void OnNavigationRequested(object? sender, InspectionModuleNavigationRequestEventArgs e)
    {
        if (!string.Equals(e.Context.TargetPageKey, InspectionModulePageKeys.AiInspectionCenter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Dispatch(() => ApplyIncomingNavigationContext(e.Context));
    }

    private void ApplyIncomingNavigationContext(InspectionModuleNavigationContext context)
    {
        var summary = _taskService.GetTaskContext(context);
        _focusedTaskId = FirstNonEmpty(context.TaskId, summary?.TaskId);
        _focusedTaskItemId = FirstNonEmpty(context.TaskItemId, summary?.TaskItemId);
        _focusedDeviceCode = FirstNonEmpty(context.DeviceCode, summary?.DeviceCode);
        _focusedEvidenceId = FirstNonEmpty(context.EvidenceId, summary?.EvidenceId);
        _focusedClosureId = FirstNonEmpty(context.ClosureId, summary?.ClosureId);

        _activeFailureReasonFilter = string.Empty;
        _quickTaskTypeFilter = string.Empty;

        Reload();

        var taskId = FirstNonEmpty(summary?.TaskId, context.TaskId);
        if (!string.IsNullOrWhiteSpace(taskId) && !TaskItems.Any(item => string.Equals(item.TaskId, taskId, StringComparison.OrdinalIgnoreCase)))
        {
            Keyword = string.Empty;
            SelectedStatus = AllStatusOption;
            Reload();
        }

        var planId = FirstNonEmpty(summary?.PlanId, context.PlanId);
        if (!string.IsNullOrWhiteSpace(planId))
        {
            SelectedPlan = TaskPlans.FirstOrDefault(item =>
                string.Equals(item.PlanId, planId, StringComparison.OrdinalIgnoreCase))
                ?? SelectedPlan;
        }

        var targetBatch = ResolveBatch(taskId);
        if (targetBatch is not null)
        {
            SelectedTask = targetBatch;
        }

        TaskCenterContextText = BuildReturnContextText(context, summary);
        RaisePropertyChanged(nameof(TaskCenterFilterText));
    }

    private IReadOnlyList<AiInspectionTaskBatch> ApplyCenterQuickFilters(IReadOnlyList<AiInspectionTaskBatch> batches)
    {
        IEnumerable<AiInspectionTaskBatch> filtered = batches;
        if (!string.IsNullOrWhiteSpace(_activeFailureReasonFilter))
        {
            filtered = filtered.Where(batch => batch.Items.Any(item => MatchesFailureCategory(batch, item, _activeFailureReasonFilter)));
        }

        return filtered.ToList();
    }

    private AiInspectionTaskOverview BuildOverviewFromBatches(IReadOnlyList<AiInspectionTaskBatch> batches)
    {
        return new AiInspectionTaskOverview
        {
            TotalTaskCount = batches.Count,
            PendingTaskCount = batches.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase)),
            RunningTaskCount = batches.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Running, StringComparison.OrdinalIgnoreCase)),
            SucceededTaskCount = batches.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Succeeded, StringComparison.OrdinalIgnoreCase)),
            FailedTaskCount = batches.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase)),
            PartiallyCompletedTaskCount = batches.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase)),
            CanceledTaskCount = batches.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Canceled, StringComparison.OrdinalIgnoreCase)),
            TotalItemCount = batches.Sum(item => item.TotalCount),
            AbnormalItemCount = batches.Sum(item => item.AbnormalCount),
            GeneratedAt = DateTimeOffset.Now,
            StatusMessage = batches.Count == 0
                ? "当前筛选条件下暂无批次。"
                : $"当前筛选到 {batches.Count} 个批次，覆盖 {batches.Sum(item => item.TotalCount)} 个点位子任务。"
        };
    }

    private IEnumerable<AiInspectionTaskItem> ApplyDetailItemFilters(AiInspectionTaskBatch batch)
    {
        IEnumerable<AiInspectionTaskItem> items = batch.Items;
        if (!string.IsNullOrWhiteSpace(_activeFailureReasonFilter))
        {
            items = items.Where(item => MatchesFailureCategory(batch, item, _activeFailureReasonFilter));
        }

        return items;
    }

    private AiInspectionTaskItem? ResolvePreferredTaskItem(AiInspectionTaskBatch batch, IReadOnlyList<AiInspectionTaskItem> filteredItems)
    {
        var preferredId = FirstNonEmpty(_focusedTaskItemId, SelectedTaskItem?.ItemId);
        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var matchedByItemId = filteredItems.FirstOrDefault(item =>
                string.Equals(item.ItemId, preferredId, StringComparison.OrdinalIgnoreCase));
            if (matchedByItemId is not null)
            {
                return matchedByItemId;
            }
        }

        if (!string.IsNullOrWhiteSpace(_focusedEvidenceId))
        {
            var matchedByEvidence = filteredItems.FirstOrDefault(item =>
                string.Equals(item.LinkedScreenshotSampleId, _focusedEvidenceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.LinkedReviewId, _focusedEvidenceId, StringComparison.OrdinalIgnoreCase));
            if (matchedByEvidence is not null)
            {
                return matchedByEvidence;
            }
        }

        if (!string.IsNullOrWhiteSpace(_focusedClosureId))
        {
            var matchedByClosure = filteredItems.FirstOrDefault(item =>
                string.Equals(item.LinkedClosureId, _focusedClosureId, StringComparison.OrdinalIgnoreCase));
            if (matchedByClosure is not null)
            {
                return matchedByClosure;
            }
        }

        if (!string.IsNullOrWhiteSpace(_focusedDeviceCode))
        {
            var matchedByDevice = filteredItems.FirstOrDefault(item =>
                string.Equals(item.DeviceCode, _focusedDeviceCode, StringComparison.OrdinalIgnoreCase));
            if (matchedByDevice is not null)
            {
                return matchedByDevice;
            }
        }

        return filteredItems.FirstOrDefault()
               ?? batch.Items
                   .OrderByDescending(item => item.IsAbnormalResult)
                   .ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
                   .FirstOrDefault();
    }

    private void OpenHistoryLatestBatch(AiInspectionTaskPlanExecutionHistory? history)
    {
        if (history is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(history.PlanId))
        {
            SelectedPlan = TaskPlans.FirstOrDefault(item =>
                string.Equals(item.PlanId, history.PlanId, StringComparison.OrdinalIgnoreCase))
                ?? SelectedPlan;
        }

        var batch = ResolveBatch(history.LatestTaskId);
        if (batch is not null)
        {
            OpenBatchDetail(batch, updateContextText: false);
            TaskCenterContextText = $"已打开计划 {history.PlanName} 的最近实例化批次。";
            return;
        }

        TaskCenterContextText = $"计划 {history.PlanName} 当前暂无可定位的实例化批次。";
    }

    private void OpenFailedPlan(AiInspectionFailedPlanSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        SelectedPlan = TaskPlans.FirstOrDefault(item =>
            string.Equals(item.PlanId, summary.PlanId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedPlan;

        var failedBatch = _taskService.GetPlanExecutionBatches(summary.PlanId)
            .FirstOrDefault(IsFailedBatchLocal);
        if (failedBatch is not null)
        {
            OpenBatchDetail(failedBatch, updateContextText: false);
        }

        TaskCenterContextText = $"已聚焦失败计划 {summary.PlanName}，可继续查看最近失败批次。";
    }

    private void OpenFailedBatch(AiInspectionFailedBatchSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var batch = ResolveBatch(summary.TaskId);
        if (batch is null)
        {
            TaskCenterContextText = $"未命中失败批次 {summary.TaskName}，已保留当前选择。";
            return;
        }

        OpenBatchDetail(batch, updateContextText: false);
        TaskCenterContextText = $"已打开失败批次 {summary.TaskName}。";
    }

    private void OpenFailedPoint(AiInspectionFailedPointSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var batch = ResolveBatch(summary.TaskId);
        if (batch is null)
        {
            TaskCenterContextText = $"未命中失败点位 {summary.DeviceCode} 对应批次，已保留当前选择。";
            return;
        }

        var item = ResolveItem(batch, summary.TaskItemId, summary.DeviceCode);
        if (item is null)
        {
            TaskCenterContextText = $"未命中失败点位 {summary.DeviceCode} 对应子任务，已保留当前选择。";
            return;
        }

        OpenBatchDetail(batch, preferredTaskItemId: item.ItemId, deviceCode: item.DeviceCode, updateContextText: false);
        var targetPageKey = ResolveFailurePointTargetPage(batch, item);
        NavigateToTargetPage(targetPageKey, batch, item);
        TaskCenterContextText = $"已按失败点位 {summary.DeviceName} 智能跳转到最合适的处理入口。";
    }

    private void ApplyFailureReasonFilter(AiInspectionFailureReasonStat? stat)
    {
        _activeFailureReasonFilter = stat?.CategoryName?.Trim() ?? string.Empty;
        _focusedTaskItemId = string.Empty;
        _focusedEvidenceId = string.Empty;
        _focusedClosureId = string.Empty;
        Reload();
        TaskCenterContextText = string.IsNullOrWhiteSpace(_activeFailureReasonFilter)
            ? "已清空失败原因过滤。"
            : $"已按失败原因“{_activeFailureReasonFilter}”筛入任务明细。";
        RaisePropertyChanged(nameof(TaskCenterFilterText));
    }

    private void ApplyTaskTypeFilter(AiInspectionTaskTypeFailureStat? stat)
    {
        _quickTaskTypeFilter = stat?.TaskType?.Trim() ?? string.Empty;
        Reload();
        TaskCenterContextText = string.IsNullOrWhiteSpace(_quickTaskTypeFilter)
            ? "已清空任务类型过滤。"
            : $"已按任务类型“{AiInspectionTaskTextMapper.ToTaskTypeText(_quickTaskTypeFilter)}”筛入任务列表。";
        RaisePropertyChanged(nameof(TaskCenterFilterText));
    }

    private void OpenRepeatedFailurePoint(AiInspectionContinuousFailurePointSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var matched = FindLatestFailedPoint(summary.DeviceCode);
        if (matched.Batch is null || matched.Item is null)
        {
            TaskCenterContextText = $"未命中重复失败点位 {summary.DeviceCode} 的来源任务，已保留当前选择。";
            return;
        }

        OpenBatchDetail(
            matched.Batch,
            preferredTaskItemId: matched.Item.ItemId,
            deviceCode: matched.Item.DeviceCode,
            updateContextText: false);
        var targetPageKey = ResolveFailurePointTargetPage(matched.Batch, matched.Item);
        NavigateToTargetPage(targetPageKey, matched.Batch, matched.Item);
        TaskCenterContextText = $"已按连续失败点位 {summary.DeviceName} 进入处理入口。";
    }

    private void OpenBatchDetail(
        AiInspectionTaskBatch? batch,
        string? preferredTaskItemId = null,
        string? deviceCode = null,
        string? evidenceId = null,
        string? closureId = null,
        bool updateContextText = true)
    {
        if (batch is null)
        {
            return;
        }

        _focusedTaskId = batch.TaskId;
        _focusedTaskItemId = preferredTaskItemId ?? string.Empty;
        _focusedDeviceCode = deviceCode ?? string.Empty;
        _focusedEvidenceId = evidenceId ?? string.Empty;
        _focusedClosureId = closureId ?? string.Empty;

        var visibleTask = TaskItems.FirstOrDefault(item =>
            string.Equals(item.TaskId, batch.TaskId, StringComparison.OrdinalIgnoreCase))
            ?? _taskService.GetDetail(batch.TaskId)
            ?? batch;

        if (!string.IsNullOrWhiteSpace(visibleTask.SourcePlanId))
        {
            SelectedPlan = TaskPlans.FirstOrDefault(item =>
                string.Equals(item.PlanId, visibleTask.SourcePlanId, StringComparison.OrdinalIgnoreCase))
                ?? SelectedPlan;
        }

        SelectedTask = visibleTask;

        if (updateContextText)
        {
            TaskCenterContextText = $"已聚焦批次 {visibleTask.TaskName}。";
        }
    }

    private void NavigateBatchToPage(
        AiInspectionTaskBatch? batch,
        string targetPageKey,
        Func<AiInspectionTaskBatch, AiInspectionTaskItem?> selector)
    {
        if (batch is null)
        {
            return;
        }

        var resolvedBatch = _taskService.GetDetail(batch.TaskId) ?? batch;
        var item = selector(resolvedBatch);
        OpenBatchDetail(
            resolvedBatch,
            preferredTaskItemId: item?.ItemId,
            deviceCode: item?.DeviceCode,
            evidenceId: FirstNonEmpty(item?.LinkedScreenshotSampleId, item?.LinkedReviewId),
            closureId: item?.LinkedClosureId,
            updateContextText: false);
        NavigateToTargetPage(targetPageKey, resolvedBatch, item);
        TaskCenterContextText = $"已从批次 {resolvedBatch.TaskName} 跳转到目标页，并保留来源任务上下文。";
    }

    private void NavigateToTargetPage(string targetPageKey, AiInspectionTaskBatch batch, AiInspectionTaskItem? contextItem)
    {
        _moduleNavigationService.Navigate(new InspectionModuleNavigationContext
        {
            TargetPageKey = targetPageKey,
            SourcePageKey = InspectionModulePageKeys.AiInspectionCenter,
            DeviceCode = contextItem?.DeviceCode ?? string.Empty,
            TaskId = batch.TaskId,
            TaskItemId = contextItem?.ItemId ?? string.Empty,
            PlanId = batch.SourcePlanId,
            EvidenceId = FirstNonEmpty(contextItem?.LinkedScreenshotSampleId, contextItem?.LinkedReviewId),
            ClosureId = contextItem?.LinkedClosureId ?? string.Empty,
            ContextSummary = $"{batch.TaskName} / {contextItem?.DeviceName ?? "--"}"
        });
    }

    private AiInspectionTaskBatch? ResolveBatch(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return null;
        }

        return _taskService.GetDetail(taskId)
               ?? TaskItems.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.OrdinalIgnoreCase))
               ?? SelectedPlanExecutionBatches.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
    }

    private static AiInspectionTaskItem? ResolveItem(AiInspectionTaskBatch batch, string? taskItemId, string? deviceCode)
    {
        if (!string.IsNullOrWhiteSpace(taskItemId))
        {
            var matchedByItemId = batch.Items.FirstOrDefault(item =>
                string.Equals(item.ItemId, taskItemId, StringComparison.OrdinalIgnoreCase));
            if (matchedByItemId is not null)
            {
                return matchedByItemId;
            }
        }

        if (!string.IsNullOrWhiteSpace(deviceCode))
        {
            var matchedByDevice = batch.Items.FirstOrDefault(item =>
                string.Equals(item.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase));
            if (matchedByDevice is not null)
            {
                return matchedByDevice;
            }
        }

        return null;
    }

    private (AiInspectionTaskBatch? Batch, AiInspectionTaskItem? Item) FindLatestFailedPoint(string? deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return (null, null);
        }

        var batch = _taskService.Query(new AiInspectionTaskQuery())
            .OrderByDescending(item => item.CompletedAt ?? item.StartedAt ?? item.CreatedAt)
            .FirstOrDefault(item => item.Items.Any(child =>
                string.Equals(child.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase) &&
                IsFailedItemLocal(child)));
        if (batch is null)
        {
            return (null, null);
        }

        var detail = _taskService.GetDetail(batch.TaskId) ?? batch;
        var item = detail.Items.FirstOrDefault(child =>
            string.Equals(child.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase) &&
            IsFailedItemLocal(child));
        return (detail, item);
    }

    private static AiInspectionTaskItem? ResolvePointNavigationItem(AiInspectionTaskBatch batch)
    {
        return batch.Items
            .OrderByDescending(item => item.IsAbnormalResult)
            .ThenByDescending(IsFailedItemLocal)
            .ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static AiInspectionTaskItem? ResolveReviewNavigationItem(AiInspectionTaskBatch batch)
    {
        return batch.Items.FirstOrDefault(item =>
                   !string.IsNullOrWhiteSpace(item.LinkedScreenshotSampleId) ||
                   !string.IsNullOrWhiteSpace(item.LinkedReviewId))
               ?? batch.Items.FirstOrDefault(item => item.IsAbnormalResult)
               ?? ResolvePointNavigationItem(batch);
    }

    private static AiInspectionTaskItem? ResolveClosureNavigationItem(AiInspectionTaskBatch batch)
    {
        return batch.Items.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.LinkedClosureId))
               ?? batch.Items.FirstOrDefault(item => item.IsAbnormalResult)
               ?? ResolvePointNavigationItem(batch);
    }

    private static bool MatchesFailureCategory(AiInspectionTaskBatch batch, AiInspectionTaskItem item, string categoryName)
    {
        return string.Equals(ResolveFailureCategory(batch, item), categoryName, StringComparison.OrdinalIgnoreCase);
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

    private static string ResolveFailureReason(AiInspectionTaskBatch batch, AiInspectionTaskItem item)
    {
        return FirstNonEmpty(
            NormalizeSingleLine(item.LastError),
            NormalizeSingleLine(item.LastResultSummary),
            NormalizeSingleLine(batch.FailureSummary),
            NormalizeSingleLine(batch.LatestResultSummary),
            "任务执行失败");
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

    private static bool IsFailedBatchLocal(AiInspectionTaskBatch batch)
    {
        return string.Equals(batch.Status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(batch.Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase) ||
               batch.Items.Any(IsFailedItemLocal);
    }

    private static bool IsFailedItemLocal(AiInspectionTaskItem item)
    {
        return string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFailurePointTargetPage(AiInspectionTaskBatch batch, AiInspectionTaskItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.LinkedClosureId))
        {
            return InspectionModulePageKeys.FaultClosure;
        }

        if (!string.IsNullOrWhiteSpace(item.LinkedScreenshotSampleId) ||
            !string.IsNullOrWhiteSpace(item.LinkedReviewId))
        {
            return InspectionModulePageKeys.ReviewCenter;
        }

        return string.Equals(batch.TaskType, AiInspectionTaskType.BasicInspection, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(batch.TaskType, AiInspectionTaskType.Recheck, StringComparison.OrdinalIgnoreCase)
            ? InspectionModulePageKeys.PointGovernance
            : InspectionModulePageKeys.MapInspection;
    }

    private static string BuildReturnContextText(
        InspectionModuleNavigationContext context,
        AiInspectionTaskContextSummary? summary)
    {
        var sourceText = context.SourcePageKey switch
        {
            InspectionModulePageKeys.MapInspection => "地图巡检台",
            InspectionModulePageKeys.PointGovernance => "点位治理中心",
            InspectionModulePageKeys.ReviewCenter => "复核中心",
            InspectionModulePageKeys.FaultClosure => "故障闭环中心",
            _ => "来源页面"
        };

        var taskText = FirstNonEmpty(summary?.TaskName, context.ContextSummary, "当前任务");
        return $"已从{sourceText}回到来源任务，当前聚焦 {taskText}。";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
