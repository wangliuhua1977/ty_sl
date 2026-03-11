using System.Collections.ObjectModel;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class AiInspectionCenterPageViewModel
{
    private AiInspectionTaskPlanExecutionHistory? _selectedPlanHistoryEntry;
    private AiInspectionFailureDashboard _failureDashboard = new();
    private bool _isSyncingPlanHistorySelection;

    public ObservableCollection<AiInspectionTaskPlanExecutionHistory> PlanHistoryItems { get; }

    public ObservableCollection<AiInspectionTaskBatch> SelectedPlanExecutionBatches { get; }

    public ObservableCollection<AiInspectionFailedPlanSummary> FailurePlanItems { get; }

    public ObservableCollection<AiInspectionFailedBatchSummary> FailureBatchItems { get; }

    public ObservableCollection<AiInspectionFailedPointSummary> FailurePointItems { get; }

    public ObservableCollection<AiInspectionFailureReasonStat> FailureReasonItems { get; }

    public ObservableCollection<AiInspectionTaskTypeFailureStat> TaskTypeFailureItems { get; }

    public ObservableCollection<AiInspectionContinuousFailurePointSummary> RepeatedFailurePointItems { get; }

    public AiInspectionTaskPlanExecutionHistory? SelectedPlanHistoryEntry
    {
        get => _selectedPlanHistoryEntry;
        set
        {
            if (!SetProperty(ref _selectedPlanHistoryEntry, value))
            {
                return;
            }

            if (_isSyncingPlanHistorySelection || value is null)
            {
                RaisePlanHistoryChanged();
                return;
            }

            var plan = TaskPlans.FirstOrDefault(item =>
                string.Equals(item.PlanId, value.PlanId, StringComparison.OrdinalIgnoreCase));
            if (plan is not null && !ReferenceEquals(SelectedPlan, plan))
            {
                SelectedPlan = plan;
                return;
            }

            RebuildSelectedPlanExecutionBatches();
        }
    }

    public string SelectedPlanHistoryName => _selectedPlanHistoryEntry?.PlanName ?? "请选择任务计划";

    public string SelectedPlanHistoryTypeText => _selectedPlanHistoryEntry is null
        ? "--"
        : $"{_selectedPlanHistoryEntry.TaskTypeText} / {_selectedPlanHistoryEntry.ScopeModeText}";

    public string SelectedPlanHistoryScheduleText => _selectedPlanHistoryEntry?.ScheduleText ?? "--";

    public string SelectedPlanHistoryLastRunText => _selectedPlanHistoryEntry?.LastRunAtText ?? "--";

    public string SelectedPlanHistoryLatestTaskText => _selectedPlanHistoryEntry?.LatestTaskText ?? "--";

    public string SelectedPlanHistoryCountersText => _selectedPlanHistoryEntry?.CountersText ?? "--";

    public string SelectedPlanHistoryPendingText => _selectedPlanHistoryEntry?.PendingText ?? "--";

    public string SelectedPlanHistoryResultText => _selectedPlanHistoryEntry?.LatestResultSummary ?? "选择计划后查看最近实例化批次结果。";

    public string SelectedPlanHistoryEnabledText => _selectedPlanHistoryEntry?.EnabledText ?? "--";

    public string FailureBoardMostFailedPlanText => _failureDashboard.MostFailedPlanText;

    public string FailureBoardMostFailedTaskTypeText => _failureDashboard.MostFailedTaskTypeText;

    public string FailureBoardMostRepeatedPointText => _failureDashboard.MostRepeatedPointText;

    private void RebuildPlanHistory(IReadOnlyList<AiInspectionTaskPlanExecutionHistory> histories)
    {
        PlanHistoryItems.Clear();
        foreach (var history in histories)
        {
            PlanHistoryItems.Add(history);
        }

        RebuildSelectedPlanExecutionBatches();
        RaisePlanHistoryChanged();
    }

    private void RebuildSelectedPlanExecutionBatches()
    {
        SelectedPlanExecutionBatches.Clear();

        if (SelectedPlan is not { PlanId.Length: > 0 } plan)
        {
            _isSyncingPlanHistorySelection = true;
            try
            {
                _selectedPlanHistoryEntry = null;
                RaisePropertyChanged(nameof(SelectedPlanHistoryEntry));
            }
            finally
            {
                _isSyncingPlanHistorySelection = false;
            }

            RaisePlanHistoryChanged();
            return;
        }

        _isSyncingPlanHistorySelection = true;
        try
        {
            _selectedPlanHistoryEntry = PlanHistoryItems.FirstOrDefault(item =>
                string.Equals(item.PlanId, plan.PlanId, StringComparison.OrdinalIgnoreCase));
            RaisePropertyChanged(nameof(SelectedPlanHistoryEntry));
        }
        finally
        {
            _isSyncingPlanHistorySelection = false;
        }

        foreach (var batch in _taskService.GetPlanExecutionBatches(plan.PlanId))
        {
            SelectedPlanExecutionBatches.Add(batch);
        }

        RaisePlanHistoryChanged();
    }

    private void RebuildFailureDashboard(AiInspectionFailureDashboard dashboard)
    {
        _failureDashboard = dashboard;

        ReplaceCollection(FailurePlanItems, dashboard.FailedPlans);
        ReplaceCollection(FailureBatchItems, dashboard.FailedBatches);
        ReplaceCollection(FailurePointItems, dashboard.FailedPoints);
        ReplaceCollection(FailureReasonItems, dashboard.FailureReasons);
        ReplaceCollection(TaskTypeFailureItems, dashboard.TaskTypeFailures);
        ReplaceCollection(RepeatedFailurePointItems, dashboard.RepeatedFailurePoints);

        RaiseFailureBoardChanged();
    }

    private void RaisePlanHistoryChanged()
    {
        RaisePropertyChanged(nameof(SelectedPlanHistoryName));
        RaisePropertyChanged(nameof(SelectedPlanHistoryTypeText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryScheduleText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryLastRunText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryLatestTaskText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryCountersText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryPendingText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryResultText));
        RaisePropertyChanged(nameof(SelectedPlanHistoryEnabledText));
    }

    private void RaiseFailureBoardChanged()
    {
        RaisePropertyChanged(nameof(FailureBoardMostFailedPlanText));
        RaisePropertyChanged(nameof(FailureBoardMostFailedTaskTypeText));
        RaisePropertyChanged(nameof(FailureBoardMostRepeatedPointText));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
