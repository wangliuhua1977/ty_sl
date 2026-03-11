using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class ReviewCenterPageViewModel
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
        if (!string.Equals(e.Context.TargetPageKey, InspectionModulePageKeys.ReviewCenter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = ApplyNavigationContextAsync(e.Context)));
    }

    private async Task ApplyNavigationContextAsync(InspectionModuleNavigationContext context)
    {
        ActivateTaskContext(context);

        var summary = _aiInspectionTaskService.GetTaskContext(context);
        if (summary is not null &&
            !string.IsNullOrWhiteSpace(summary.SchemeId) &&
            !string.Equals(_inspectionScopeService.GetCurrentScheme().Id, summary.SchemeId, StringComparison.OrdinalIgnoreCase))
        {
            _suppressScopeEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.SetCurrentScheme(summary.SchemeId));
            }
            catch
            {
            }
            finally
            {
                _suppressScopeEvent = false;
            }

            await RefreshOverviewAsync(forceRefreshAiAlerts: false, preserveSelection: false);
        }

        var evidenceId = FirstNonEmpty(context.EvidenceId, summary?.EvidenceId);
        if (!string.IsNullOrWhiteSpace(evidenceId))
        {
            var matchedEvidence = _allEvidenceItems.FirstOrDefault(item =>
                string.Equals(item.EvidenceId, evidenceId, StringComparison.OrdinalIgnoreCase));
            if (matchedEvidence is not null)
            {
                SelectEvidence(matchedEvidence, publishSelection: true);
                TaskContextLocateText = $"已按来源证据定位复核对象 {matchedEvidence.EvidenceId}。";
                return;
            }
        }

        var targetDeviceCode = FirstNonEmpty(summary?.DeviceCode, context.DeviceCode);
        if (!string.IsNullOrWhiteSpace(targetDeviceCode) && TrySelectDevice(targetDeviceCode, publishSelection: true))
        {
            RefreshSourceTaskContext(targetDeviceCode);
            TaskContextLocateText = string.IsNullOrWhiteSpace(evidenceId)
                ? $"已按任务点位定位复核对象 {targetDeviceCode}。"
                : $"未命中原始证据，已回退到点位 {targetDeviceCode} 的当前复核对象。";
            return;
        }

        RefreshSourceTaskContext(null);
        TaskContextLocateText = "未命中原始证据，已保留当前复核上下文。";
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
            TargetPageKey = InspectionModulePageKeys.ReviewCenter,
            TaskId = _activeTaskId,
            TaskItemId = _activeTaskItemId,
            DeviceCode = deviceCode ?? string.Empty,
            PlanId = _activePlanId,
            EvidenceId = _activeEvidenceId,
            ClosureId = _activeClosureId
        });

        RaiseSourceTaskContextChanged();
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
            SourcePageKey = InspectionModulePageKeys.ReviewCenter,
            DeviceCode = FirstNonEmpty(_sourceTaskContext?.DeviceCode, SelectedDeviceCode),
            TaskId = FirstNonEmpty(_activeTaskId, _sourceTaskContext?.TaskId),
            TaskItemId = FirstNonEmpty(_activeTaskItemId, _sourceTaskContext?.TaskItemId),
            PlanId = FirstNonEmpty(_activePlanId, _sourceTaskContext?.PlanId),
            EvidenceId = FirstNonEmpty(_activeEvidenceId, _sourceTaskContext?.EvidenceId),
            ClosureId = FirstNonEmpty(_activeClosureId, _sourceTaskContext?.ClosureId),
            ContextSummary = FirstNonEmpty(_sourceTaskContext?.TaskName, "返回来源任务")
        });
    }
}
