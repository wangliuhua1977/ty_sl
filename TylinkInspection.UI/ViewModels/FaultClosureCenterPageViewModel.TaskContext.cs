using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class FaultClosureCenterPageViewModel
{
    private readonly IInspectionModuleNavigationService _moduleNavigationService;
    private readonly IAiInspectionTaskService _aiInspectionTaskService;

    private AiInspectionTaskContextSummary? _sourceTaskContext;
    private string _activeTaskId = string.Empty;
    private string _activeTaskItemId = string.Empty;
    private string _activeEvidenceId = string.Empty;
    private string _activeClosureId = string.Empty;
    private string _activePlanId = string.Empty;
    private string _taskContextLocateText = "当前未承接来源任务。";
    private ICommand? _returnToSourceTaskCommand;

    public string SourceTaskNameText => _sourceTaskContext?.TaskName ?? "--";

    public string SourceTaskTypeText => _sourceTaskContext is null
        ? "--"
        : $"{_sourceTaskContext.TaskTypeText} / 点位 {_sourceTaskContext.DeviceName}";

    public string SourceTaskExecutionText => _sourceTaskContext?.ExecutionWindowText ?? "--";

    public string SourceTaskPlanText => _sourceTaskContext?.PlanText ?? "--";

    public string SourceTaskOriginText => _sourceTaskContext?.SourceDisplayText ?? "--";

    public string SourceTaskStatusText => _sourceTaskContext is null
        ? "--"
        : $"{_sourceTaskContext.TaskStatusText} / 子任务 {_sourceTaskContext.ItemStatusText}";

    public string SourceTaskPointResultText => _sourceTaskContext?.PointResultText ?? "--";

    public string SourceTaskFailureText => _sourceTaskContext?.FailureOrAbnormalText ?? "--";

    public bool CanReturnToSourceTask => HasActiveTaskContext();

    public ICommand ReturnToSourceTaskCommand => _returnToSourceTaskCommand ??=
        new RelayCommand<object?>(_ => ReturnToSourceTask());

    public string TaskContextLocateText
    {
        get => _taskContextLocateText;
        private set => SetProperty(ref _taskContextLocateText, value);
    }

    private void OnNavigationRequested(object? sender, InspectionModuleNavigationRequestEventArgs e)
    {
        if (!string.Equals(e.Context.TargetPageKey, InspectionModulePageKeys.FaultClosure, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = ApplyNavigationContextAsync(e.Context)));
    }

    private Task ApplyNavigationContextAsync(InspectionModuleNavigationContext context)
    {
        ActivateTaskContext(context);

        var summary = _aiInspectionTaskService.GetTaskContext(context);
        if (summary is not null)
        {
            _activeTaskId = FirstNonEmpty(_activeTaskId, summary.TaskId);
            _activeTaskItemId = FirstNonEmpty(_activeTaskItemId, summary.TaskItemId);
            _activeEvidenceId = FirstNonEmpty(_activeEvidenceId, summary.EvidenceId);
            _activeClosureId = FirstNonEmpty(_activeClosureId, summary.ClosureId);
            _activePlanId = FirstNonEmpty(_activePlanId, summary.PlanId);
        }

        var matchedRecord = FindRecordByContext(summary, context);
        if (matchedRecord is not null)
        {
            if (!ReferenceEquals(SelectedRecord, matchedRecord))
            {
                SelectedRecord = matchedRecord;
            }
            else
            {
                RefreshSourceTaskContext(matchedRecord.DeviceCode);
            }

            TaskContextLocateText = !string.IsNullOrWhiteSpace(_activeClosureId)
                ? $"已按闭环记录定位 {matchedRecord.RecordId}。"
                : !string.IsNullOrWhiteSpace(_activeEvidenceId)
                    ? $"已按来源证据关联闭环记录 {matchedRecord.RecordId}。"
                    : $"已按任务上下文定位到闭环记录 {matchedRecord.RecordId}。";
            return Task.CompletedTask;
        }

        RefreshSourceTaskContext(FirstNonEmpty(summary?.DeviceCode, context.DeviceCode));
        TaskContextLocateText = !string.IsNullOrWhiteSpace(_activeClosureId)
            ? $"未在当前闭环列表命中记录 {_activeClosureId}，已保留当前选择。"
            : !string.IsNullOrWhiteSpace(_activeEvidenceId)
                ? "未在当前闭环列表命中来源证据关联记录，已保留当前选择。"
                : "未在当前闭环列表命中来源任务记录，已保留当前选择。";
        return Task.CompletedTask;
    }

    private void ActivateTaskContext(InspectionModuleNavigationContext context)
    {
        _activeTaskId = context.TaskId ?? string.Empty;
        _activeTaskItemId = context.TaskItemId ?? string.Empty;
        _activeEvidenceId = context.EvidenceId ?? string.Empty;
        _activeClosureId = context.ClosureId ?? string.Empty;
        _activePlanId = context.PlanId ?? string.Empty;
    }

    private void RefreshSourceTaskContext(string? deviceCode)
    {
        if (!HasActiveTaskContext())
        {
            _sourceTaskContext = null;
            TaskContextLocateText = "当前未承接来源任务。";
            RaiseSourceTaskContextChanged();
            return;
        }

        _sourceTaskContext = _aiInspectionTaskService.GetTaskContext(new InspectionModuleNavigationContext
        {
            TargetPageKey = InspectionModulePageKeys.FaultClosure,
            TaskId = _activeTaskId,
            TaskItemId = _activeTaskItemId,
            DeviceCode = deviceCode ?? string.Empty,
            PlanId = _activePlanId,
            EvidenceId = _activeEvidenceId,
            ClosureId = _activeClosureId
        });

        RaiseSourceTaskContextChanged();
    }

    private FaultClosureRecord? FindRecordByContext(
        AiInspectionTaskContextSummary? summary,
        InspectionModuleNavigationContext context)
    {
        var closureId = FirstNonEmpty(_activeClosureId, context.ClosureId, summary?.ClosureId);
        if (!string.IsNullOrWhiteSpace(closureId))
        {
            var matchedByClosure = Records.FirstOrDefault(item =>
                string.Equals(item.RecordId, closureId, StringComparison.OrdinalIgnoreCase));
            if (matchedByClosure is not null)
            {
                return matchedByClosure;
            }
        }

        var evidenceId = FirstNonEmpty(_activeEvidenceId, context.EvidenceId, summary?.EvidenceId);
        if (!string.IsNullOrWhiteSpace(evidenceId))
        {
            var matchedByEvidence = Records.FirstOrDefault(item =>
                string.Equals(item.RelatedEvidenceId, evidenceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.RelatedManualReviewId, evidenceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.RelatedScreenshotSampleId, evidenceId, StringComparison.OrdinalIgnoreCase));
            if (matchedByEvidence is not null)
            {
                return matchedByEvidence;
            }
        }

        var targetDeviceCode = FirstNonEmpty(summary?.DeviceCode, context.DeviceCode);
        if (!string.IsNullOrWhiteSpace(targetDeviceCode))
        {
            return Records.FirstOrDefault(item =>
                string.Equals(item.DeviceCode, targetDeviceCode, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private void RaiseSourceTaskContextChanged()
    {
        RaisePropertyChanged(nameof(SourceTaskNameText));
        RaisePropertyChanged(nameof(SourceTaskTypeText));
        RaisePropertyChanged(nameof(SourceTaskExecutionText));
        RaisePropertyChanged(nameof(SourceTaskPlanText));
        RaisePropertyChanged(nameof(SourceTaskOriginText));
        RaisePropertyChanged(nameof(SourceTaskStatusText));
        RaisePropertyChanged(nameof(SourceTaskPointResultText));
        RaisePropertyChanged(nameof(SourceTaskFailureText));
        RaisePropertyChanged(nameof(CanReturnToSourceTask));
    }

    private bool HasActiveTaskContext()
    {
        return !string.IsNullOrWhiteSpace(_activeTaskId) ||
               !string.IsNullOrWhiteSpace(_activeTaskItemId) ||
               !string.IsNullOrWhiteSpace(_activeEvidenceId) ||
               !string.IsNullOrWhiteSpace(_activeClosureId);
    }

    private void ReturnToSourceTask()
    {
        if (!HasActiveTaskContext())
        {
            return;
        }

        _moduleNavigationService.Navigate(new InspectionModuleNavigationContext
        {
            TargetPageKey = InspectionModulePageKeys.AiInspectionCenter,
            SourcePageKey = InspectionModulePageKeys.FaultClosure,
            DeviceCode = FirstNonEmpty(_sourceTaskContext?.DeviceCode, SelectedRecord?.DeviceCode),
            TaskId = FirstNonEmpty(_activeTaskId, _sourceTaskContext?.TaskId),
            TaskItemId = FirstNonEmpty(_activeTaskItemId, _sourceTaskContext?.TaskItemId),
            PlanId = FirstNonEmpty(_activePlanId, _sourceTaskContext?.PlanId),
            EvidenceId = FirstNonEmpty(_activeEvidenceId, _sourceTaskContext?.EvidenceId),
            ClosureId = FirstNonEmpty(_activeClosureId, _sourceTaskContext?.ClosureId),
            ContextSummary = FirstNonEmpty(_sourceTaskContext?.TaskName, "返回来源任务")
        });
    }
}
