using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class PointGovernancePageViewModel
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
        if (!string.Equals(e.Context.TargetPageKey, InspectionModulePageKeys.PointGovernance, StringComparison.OrdinalIgnoreCase))
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
            _suppressServiceEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.SetCurrentScheme(summary.SchemeId));
            }
            catch
            {
            }
            finally
            {
                _suppressServiceEvent = false;
            }

            IsCatalogPoolMode = false;
            await ReloadScopeDataAsync(preserveSelection: true);
        }

        var targetDeviceCode = FirstNonEmpty(summary?.DeviceCode, context.DeviceCode);
        if (string.IsNullOrWhiteSpace(targetDeviceCode))
        {
            RefreshSourceTaskContext(null);
            TaskContextLocateText = "未命中来源任务点位，已保留当前治理视图。";
            return;
        }

        TryApplySharedSelection(targetDeviceCode);
        RefreshSourceTaskContext(targetDeviceCode);

        TaskContextLocateText = SelectedDevice is null ||
                                !string.Equals(SelectedDevice.DeviceCode, targetDeviceCode, StringComparison.OrdinalIgnoreCase)
            ? $"未精确命中点位 {targetDeviceCode}，已保留当前治理上下文。"
            : $"已按任务上下文定位到点位 {targetDeviceCode}。";
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
            TargetPageKey = InspectionModulePageKeys.PointGovernance,
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
            SourcePageKey = InspectionModulePageKeys.PointGovernance,
            DeviceCode = FirstNonEmpty(_sourceTaskContext?.DeviceCode, SelectedDevice?.DeviceCode),
            TaskId = FirstNonEmpty(_activeTaskId, _sourceTaskContext?.TaskId),
            TaskItemId = FirstNonEmpty(_activeTaskItemId, _sourceTaskContext?.TaskItemId),
            PlanId = FirstNonEmpty(_activePlanId, _sourceTaskContext?.PlanId),
            EvidenceId = FirstNonEmpty(_activeEvidenceId, _sourceTaskContext?.EvidenceId),
            ClosureId = FirstNonEmpty(_activeClosureId, _sourceTaskContext?.ClosureId),
            ContextSummary = FirstNonEmpty(_sourceTaskContext?.TaskName, "返回来源任务")
        });
    }
}
