using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class AiInspectionCenterPageViewModel : PageViewModelBase
{
    private const string AllStatusOption = "全部状态";

    private readonly IAiInspectionTaskService _taskService;
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IInspectionModuleNavigationService _moduleNavigationService;

    private AiInspectionTaskBatch? _selectedTask;
    private AiInspectionTaskItem? _selectedTaskItem;
    private AiInspectionTaskPlan? _selectedPlan;
    private string _taskName = string.Empty;
    private string _planName = string.Empty;
    private string _planHourText = "09";
    private string _planMinuteText = "00";
    private string _selectedCreateTaskType = AiInspectionTaskType.BasicInspection;
    private string _selectedCreateScopeMode = AiInspectionTaskScopeMode.FullScheme;
    private string _selectedPlanTaskType = AiInspectionTaskType.BasicInspection;
    private string _selectedPlanScopeMode = AiInspectionTaskScopeMode.FullScheme;
    private string _selectedStatus = AllStatusOption;
    private string _keyword = string.Empty;
    private string _operatorName = Environment.UserName;
    private string _currentSchemeName = "--";
    private string _currentSchemeSummary = "当前未加载巡检范围。";
    private string _selectedTaskProgressText = "--";
    private string _selectedTaskFailureSummary = "暂无失败摘要。";
    private string _selectedTaskLatestSummary = "暂无结果摘要。";
    private string _selectedTaskResultHeadline = "请先选择任务批次。";

    public AiInspectionCenterPageViewModel(
        IAiInspectionTaskService taskService,
        IInspectionScopeService inspectionScopeService,
        IInspectionModuleNavigationService moduleNavigationService)
        : base("AI智能巡检中心", "围绕批量任务、计划实例化、结果汇总、重试重跑和跨模块联动，升级为正式任务总控页。")
    {
        _taskService = taskService;
        _inspectionScopeService = inspectionScopeService;
        _moduleNavigationService = moduleNavigationService;

        SummaryCards = new ObservableCollection<OverviewMetric>();
        TaskItems = new ObservableCollection<AiInspectionTaskBatch>();
        DetailItems = new ObservableCollection<AiInspectionTaskItem>();
        ExecutionRecords = new ObservableCollection<AiInspectionTaskExecutionRecord>();
        TaskPlans = new ObservableCollection<AiInspectionTaskPlan>();
        PlanHistoryItems = new ObservableCollection<AiInspectionTaskPlanExecutionHistory>();
        SelectedPlanExecutionBatches = new ObservableCollection<AiInspectionTaskBatch>();
        FailurePlanItems = new ObservableCollection<AiInspectionFailedPlanSummary>();
        FailureBatchItems = new ObservableCollection<AiInspectionFailedBatchSummary>();
        FailurePointItems = new ObservableCollection<AiInspectionFailedPointSummary>();
        FailureReasonItems = new ObservableCollection<AiInspectionFailureReasonStat>();
        TaskTypeFailureItems = new ObservableCollection<AiInspectionTaskTypeFailureStat>();
        RepeatedFailurePointItems = new ObservableCollection<AiInspectionContinuousFailurePointSummary>();

        StatusOptions =
        [
            AllStatusOption,
            AiInspectionTaskStatus.Pending,
            AiInspectionTaskStatus.Running,
            AiInspectionTaskStatus.Succeeded,
            AiInspectionTaskStatus.Failed,
            AiInspectionTaskStatus.PartiallyCompleted,
            AiInspectionTaskStatus.Canceled
        ];

        TaskTypeOptions =
        [
            new SelectionItemViewModel { Key = AiInspectionTaskType.BasicInspection, Title = AiInspectionTaskTextMapper.ToTaskTypeText(AiInspectionTaskType.BasicInspection) },
            new SelectionItemViewModel { Key = AiInspectionTaskType.PlaybackReview, Title = AiInspectionTaskTextMapper.ToTaskTypeText(AiInspectionTaskType.PlaybackReview) },
            new SelectionItemViewModel { Key = AiInspectionTaskType.ScreenshotReviewPreparation, Title = AiInspectionTaskTextMapper.ToTaskTypeText(AiInspectionTaskType.ScreenshotReviewPreparation) },
            new SelectionItemViewModel { Key = AiInspectionTaskType.Recheck, Title = AiInspectionTaskTextMapper.ToTaskTypeText(AiInspectionTaskType.Recheck) }
        ];

        ScopeModeOptions =
        [
            new SelectionItemViewModel { Key = AiInspectionTaskScopeMode.FullScheme, Title = AiInspectionTaskTextMapper.ToScopeModeText(AiInspectionTaskScopeMode.FullScheme) },
            new SelectionItemViewModel { Key = AiInspectionTaskScopeMode.AbnormalOnly, Title = AiInspectionTaskTextMapper.ToScopeModeText(AiInspectionTaskScopeMode.AbnormalOnly) },
            new SelectionItemViewModel { Key = AiInspectionTaskScopeMode.FocusedOnly, Title = AiInspectionTaskTextMapper.ToScopeModeText(AiInspectionTaskScopeMode.FocusedOnly) },
            new SelectionItemViewModel { Key = AiInspectionTaskScopeMode.PendingRecheckOnly, Title = AiInspectionTaskTextMapper.ToScopeModeText(AiInspectionTaskScopeMode.PendingRecheckOnly) }
        ];

        CreateAndRunCommand = new RelayCommand<object?>(_ => CreateAndRunTask());
        CreateDailyPlanCommand = new RelayCommand<object?>(_ => CreateDailyPlan());
        ToggleSelectedPlanEnabledCommand = new RelayCommand<object?>(_ => ToggleSelectedPlanEnabled());
        StartSelectedTaskCommand = new RelayCommand<object?>(_ => StartSelectedTask());
        CancelSelectedTaskCommand = new RelayCommand<object?>(_ => CancelSelectedTask());
        RetrySelectedItemCommand = new RelayCommand<object?>(_ => RetrySelectedItem());
        RerunFailedItemsCommand = new RelayCommand<object?>(_ => RerunFailedItems());
        RerunUnsuccessfulItemsCommand = new RelayCommand<object?>(_ => RerunUnsuccessfulItems());
        NavigateToPointGovernanceCommand = new RelayCommand<object?>(_ => NavigateTo(InspectionModulePageKeys.PointGovernance));
        NavigateToMapInspectionCommand = new RelayCommand<object?>(_ => NavigateTo(InspectionModulePageKeys.MapInspection));
        NavigateToReviewCenterCommand = new RelayCommand<object?>(_ => NavigateTo(InspectionModulePageKeys.ReviewCenter));
        NavigateToFaultClosureCommand = new RelayCommand<object?>(_ => NavigateTo(InspectionModulePageKeys.FaultClosure));
        RefreshCommand = new RelayCommand<object?>(_ => Reload());
        ClearFilterCommand = new RelayCommand<object?>(_ => ClearFilter());

        _taskService.TasksChanged += OnTasksChanged;
        _inspectionScopeService.ScopeChanged += OnScopeChanged;
        _moduleNavigationService.NavigationRequested += OnNavigationRequested;

        Reload();
    }

    public IReadOnlyList<string> StatusOptions { get; }

    public IReadOnlyList<SelectionItemViewModel> TaskTypeOptions { get; }

    public IReadOnlyList<SelectionItemViewModel> ScopeModeOptions { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<AiInspectionTaskBatch> TaskItems { get; }

    public ObservableCollection<AiInspectionTaskItem> DetailItems { get; }

    public ObservableCollection<AiInspectionTaskExecutionRecord> ExecutionRecords { get; }

    public ObservableCollection<AiInspectionTaskPlan> TaskPlans { get; }

    public ICommand CreateAndRunCommand { get; }

    public ICommand CreateDailyPlanCommand { get; }

    public ICommand ToggleSelectedPlanEnabledCommand { get; }

    public ICommand StartSelectedTaskCommand { get; }

    public ICommand CancelSelectedTaskCommand { get; }

    public ICommand RetrySelectedItemCommand { get; }

    public ICommand RerunFailedItemsCommand { get; }

    public ICommand RerunUnsuccessfulItemsCommand { get; }

    public ICommand NavigateToPointGovernanceCommand { get; }

    public ICommand NavigateToMapInspectionCommand { get; }

    public ICommand NavigateToReviewCenterCommand { get; }

    public ICommand NavigateToFaultClosureCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ClearFilterCommand { get; }

    public string TaskName
    {
        get => _taskName;
        set => SetProperty(ref _taskName, value);
    }

    public string PlanName
    {
        get => _planName;
        set => SetProperty(ref _planName, value);
    }

    public string PlanHourText
    {
        get => _planHourText;
        set => SetProperty(ref _planHourText, value);
    }

    public string PlanMinuteText
    {
        get => _planMinuteText;
        set => SetProperty(ref _planMinuteText, value);
    }

    public string SelectedCreateTaskType
    {
        get => _selectedCreateTaskType;
        set => SetProperty(ref _selectedCreateTaskType, value);
    }

    public string SelectedCreateScopeMode
    {
        get => _selectedCreateScopeMode;
        set => SetProperty(ref _selectedCreateScopeMode, value);
    }

    public string SelectedPlanTaskType
    {
        get => _selectedPlanTaskType;
        set => SetProperty(ref _selectedPlanTaskType, value);
    }

    public string SelectedPlanScopeMode
    {
        get => _selectedPlanScopeMode;
        set => SetProperty(ref _selectedPlanScopeMode, value);
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value);
    }

    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value);
    }

    public string CurrentSchemeName
    {
        get => _currentSchemeName;
        private set => SetProperty(ref _currentSchemeName, value);
    }

    public string CurrentSchemeSummary
    {
        get => _currentSchemeSummary;
        private set => SetProperty(ref _currentSchemeSummary, value);
    }

    public AiInspectionTaskBatch? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                RebuildDetail();
                RaiseSelectionProperties();
            }
        }
    }

    public AiInspectionTaskItem? SelectedTaskItem
    {
        get => _selectedTaskItem;
        set => SetProperty(ref _selectedTaskItem, value);
    }

    public AiInspectionTaskPlan? SelectedPlan
    {
        get => _selectedPlan;
        set
        {
            if (SetProperty(ref _selectedPlan, value))
            {
                RebuildSelectedPlanExecutionBatches();
                RaisePropertyChanged(nameof(SelectedPlanStatusText));
                RaisePropertyChanged(nameof(SelectedPlanNextRunText));
            }
        }
    }

    public string SelectedTaskProgressText
    {
        get => _selectedTaskProgressText;
        private set => SetProperty(ref _selectedTaskProgressText, value);
    }

    public string SelectedTaskFailureSummary
    {
        get => _selectedTaskFailureSummary;
        private set => SetProperty(ref _selectedTaskFailureSummary, value);
    }

    public string SelectedTaskLatestSummary
    {
        get => _selectedTaskLatestSummary;
        private set => SetProperty(ref _selectedTaskLatestSummary, value);
    }

    public string SelectedTaskResultHeadline
    {
        get => _selectedTaskResultHeadline;
        private set => SetProperty(ref _selectedTaskResultHeadline, value);
    }

    public string SelectedTaskStatusText => SelectedTask?.StatusText ?? "未选择任务";

    public string SelectedTaskTypeText => SelectedTask?.TaskTypeText ?? "--";

    public string SelectedTaskScopeText => SelectedTask?.ScopeModeText ?? "--";

    public string SelectedTaskSourceText => SelectedTask?.SourceText ?? "--";

    public string SelectedTaskPlanText => SelectedTask is null
        ? "--"
        : !string.IsNullOrWhiteSpace(SelectedTask.SourcePlanName)
            ? $"{SelectedTask.SourcePlanName} / {SelectedTask.SourcePlanId}"
            : "当前批次未关联计划";

    public string SelectedTaskSchemeText => SelectedTask is null ? "--" : $"{SelectedTask.SchemeName} / {SelectedTask.SchemeId}";

    public string SelectedTaskCreatedText => SelectedTask?.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedTaskStartedText => SelectedTask?.StartedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedTaskCompletedText => SelectedTask?.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedTaskCountsText => SelectedTask is null
        ? "--"
        : $"总数 {SelectedTask.ResultSummary.TotalCount} / 成功 {SelectedTask.ResultSummary.SuccessCount} / 失败 {SelectedTask.ResultSummary.FailedCount} / 异常 {SelectedTask.ResultSummary.AbnormalCount}";

    public string SelectedTaskPendingText => SelectedTask is null
        ? "--"
        : $"待人工复核 {SelectedTask.ResultSummary.PendingManualReviewCount} / 待闭环 {SelectedTask.ResultSummary.PendingClosureCount}";

    public string SelectedTaskBasicSummary => SelectedTask?.ResultSummary.InspectionSummaryText ?? "--";

    public string SelectedTaskPlaybackSummary => SelectedTask?.ResultSummary.PlaybackSummaryText ?? "--";

    public string SelectedTaskScreenshotSummary => SelectedTask?.ResultSummary.ScreenshotSummaryText ?? "--";

    public string SelectedTaskRecheckSummary => SelectedTask?.ResultSummary.RecheckSummaryText ?? "--";

    public string SelectedTaskClosureSummary => SelectedTask?.ResultSummary.ClosureSummaryText ?? "--";

    public string SelectedTaskAccentResourceKey => SelectedTask?.AccentResourceKey ?? "TonePrimaryBrush";

    public string SelectedPlanStatusText => SelectedPlan?.EnabledText ?? "--";

    public string SelectedPlanNextRunText => SelectedPlan?.NextRunAtText ?? "--";

    private void Reload()
    {
        var query = BuildQuery();
        var items = ApplyCenterQuickFilters(_taskService.Query(query).ToList());
        var plans = _taskService.GetPlans();
        var planHistories = _taskService.GetPlanExecutionHistory();
        var failureDashboard = _taskService.GetFailureDashboard();
        var selectedTaskId = SelectedTask?.TaskId;
        var selectedPlanId = SelectedPlan?.PlanId;

        RebuildScopeInfo();
        RebuildSummary(BuildOverviewFromBatches(items));

        TaskItems.Clear();
        foreach (var item in items)
        {
            TaskItems.Add(item);
        }

        TaskPlans.Clear();
        foreach (var plan in plans)
        {
            TaskPlans.Add(plan);
        }

        RebuildPlanHistory(planHistories);
        RebuildFailureDashboard(failureDashboard);

        SelectedTask = items.FirstOrDefault(item => item.TaskId == selectedTaskId) ?? items.FirstOrDefault();
        SelectedPlan = plans.FirstOrDefault(item => string.Equals(item.PlanId, selectedPlanId, StringComparison.OrdinalIgnoreCase))
            ?? plans.FirstOrDefault();
        RaisePropertyChanged(nameof(TaskCenterFilterText));
    }

    private void RebuildScopeInfo()
    {
        var scope = _inspectionScopeService.GetCurrentScope();
        CurrentSchemeName = $"{scope.CurrentScheme.Name} ({scope.CurrentScheme.Id})";
        CurrentSchemeSummary =
            $"覆盖点位 {scope.Summary.CoveredPointCount} / 在线 {scope.Summary.OnlinePointCount} / 异常待复检 {scope.Devices.Count(item => item.NeedRecheck)} / 重点关注 {scope.Summary.FocusPointCount}";
    }

    private void RebuildSummary(AiInspectionTaskOverview overview)
    {
        SummaryCards.Clear();
        SummaryCards.Add(BuildMetric("批次总数", overview.TotalTaskCount, "个", "全部历史批次", "TonePrimaryBrush"));
        SummaryCards.Add(BuildMetric("执行中", overview.RunningTaskCount, "个", "当前队列中", "ToneFocusBrush"));
        SummaryCards.Add(BuildMetric("待执行", overview.PendingTaskCount, "个", "待运行批次", "ToneWarningBrush"));
        SummaryCards.Add(BuildMetric("异常点位", overview.AbnormalItemCount, "个", "批次累计异常", "ToneDangerBrush"));
    }

    private void RebuildDetail()
    {
        DetailItems.Clear();
        ExecutionRecords.Clear();
        SelectedTaskItem = null;

        if (SelectedTask is null)
        {
            SelectedTaskProgressText = "--";
            SelectedTaskFailureSummary = "暂无失败摘要。";
            SelectedTaskLatestSummary = "暂无结果摘要。";
            SelectedTaskResultHeadline = "请先选择任务批次。";
            RaiseResultSummaryProperties();
            return;
        }

        var detail = _taskService.GetDetail(SelectedTask.TaskId) ?? SelectedTask;
        _selectedTask = detail;

        var filteredItems = ApplyDetailItemFilters(detail)
            .OrderByDescending(item => item.IsAbnormalResult)
            .ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var item in filteredItems)
        {
            DetailItems.Add(item);
        }

        foreach (var record in detail.ExecutionRecords.OrderByDescending(item => item.Timestamp).Take(30))
        {
            ExecutionRecords.Add(record);
        }

        SelectedTaskItem = ResolvePreferredTaskItem(detail, filteredItems);
        SelectedTaskProgressText = $"{detail.ProgressText} / 成功 {detail.SucceededCount} / 失败 {detail.FailedCount} / 取消 {detail.CanceledCount}";
        SelectedTaskFailureSummary = string.IsNullOrWhiteSpace(detail.FailureSummary) ? "暂无失败摘要。" : detail.FailureSummary;
        SelectedTaskLatestSummary = string.IsNullOrWhiteSpace(detail.LatestResultSummary) ? "暂无结果摘要。" : detail.LatestResultSummary;
        SelectedTaskResultHeadline = $"结果汇总生成时间 {detail.ResultSummary.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        RaiseResultSummaryProperties();
    }

    private void CreateAndRunTask()
    {
        try
        {
            var currentScheme = _inspectionScopeService.GetCurrentScheme();
            _taskService.CreateTask(new AiInspectionTaskCreateRequest
            {
                TaskName = TaskName,
                SchemeId = currentScheme.Id,
                TaskType = SelectedCreateTaskType,
                ScopeMode = SelectedCreateScopeMode,
                CreatedBy = OperatorName,
                ExecuteImmediately = true
            });

            TaskName = string.Empty;
            Reload();
        }
        catch (Exception ex)
        {
            SelectedTaskLatestSummary = ex.Message;
        }
    }

    private void CreateDailyPlan()
    {
        try
        {
            var currentScheme = _inspectionScopeService.GetCurrentScheme();
            _taskService.CreatePlan(new AiInspectionTaskPlanCreateRequest
            {
                PlanName = PlanName,
                SchemeId = currentScheme.Id,
                TaskType = SelectedPlanTaskType,
                ScopeMode = SelectedPlanScopeMode,
                ScheduleType = AiInspectionTaskPlanScheduleType.Daily,
                DailyHour = ParseHour(PlanHourText),
                DailyMinute = ParseMinute(PlanMinuteText),
                IsEnabled = true,
                CreatedBy = OperatorName
            });

            PlanName = string.Empty;
            Reload();
        }
        catch (Exception ex)
        {
            SelectedTaskLatestSummary = ex.Message;
        }
    }

    private void ToggleSelectedPlanEnabled()
    {
        if (SelectedPlan is null)
        {
            return;
        }

        try
        {
            _taskService.SetPlanEnabled(SelectedPlan.PlanId, !SelectedPlan.IsEnabled, OperatorName);
            Reload();
        }
        catch (Exception ex)
        {
            SelectedTaskLatestSummary = ex.Message;
        }
    }

    private void StartSelectedTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        _taskService.StartTask(SelectedTask.TaskId, OperatorName);
        Reload();
    }

    private void CancelSelectedTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        _taskService.CancelTask(SelectedTask.TaskId, OperatorName, "由任务总控页取消未完成子任务。");
        Reload();
    }

    private void RetrySelectedItem()
    {
        if (SelectedTask is null || SelectedTaskItem is null)
        {
            return;
        }

        try
        {
            _taskService.RetryTaskItem(SelectedTask.TaskId, SelectedTaskItem.ItemId, OperatorName);
            Reload();
        }
        catch (Exception ex)
        {
            SelectedTaskLatestSummary = ex.Message;
        }
    }

    private void RerunFailedItems()
    {
        if (SelectedTask is null)
        {
            return;
        }

        try
        {
            _taskService.RerunFailedItems(SelectedTask.TaskId, OperatorName);
            Reload();
        }
        catch (Exception ex)
        {
            SelectedTaskLatestSummary = ex.Message;
        }
    }

    private void RerunUnsuccessfulItems()
    {
        if (SelectedTask is null)
        {
            return;
        }

        try
        {
            _taskService.RerunUnsuccessfulItems(SelectedTask.TaskId, OperatorName);
            Reload();
        }
        catch (Exception ex)
        {
            SelectedTaskLatestSummary = ex.Message;
        }
    }

    private void NavigateTo(string targetPageKey)
    {
        if (SelectedTask is null)
        {
            return;
        }

        var contextItem = SelectedTaskItem ?? SelectedTask.Items.FirstOrDefault();
        NavigateToTargetPage(targetPageKey, SelectedTask, contextItem);
    }

    private void ClearFilter()
    {
        Keyword = string.Empty;
        SelectedStatus = AllStatusOption;
        _activeFailureReasonFilter = string.Empty;
        _quickTaskTypeFilter = string.Empty;
        _focusedTaskId = string.Empty;
        _focusedTaskItemId = string.Empty;
        _focusedDeviceCode = string.Empty;
        _focusedEvidenceId = string.Empty;
        _focusedClosureId = string.Empty;
        TaskCenterContextText = "已清空任务中心筛选与回流上下文。";
        Reload();
    }

    private AiInspectionTaskQuery BuildQuery()
    {
        return new AiInspectionTaskQuery
        {
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(),
            Status = SelectedStatus == AllStatusOption ? null : SelectedStatus,
            TaskType = string.IsNullOrWhiteSpace(_quickTaskTypeFilter) ? null : _quickTaskTypeFilter
        };
    }

    private void RaiseSelectionProperties()
    {
        RaisePropertyChanged(nameof(SelectedTaskStatusText));
        RaisePropertyChanged(nameof(SelectedTaskTypeText));
        RaisePropertyChanged(nameof(SelectedTaskScopeText));
        RaisePropertyChanged(nameof(SelectedTaskSourceText));
        RaisePropertyChanged(nameof(SelectedTaskPlanText));
        RaisePropertyChanged(nameof(SelectedTaskSchemeText));
        RaisePropertyChanged(nameof(SelectedTaskCreatedText));
        RaisePropertyChanged(nameof(SelectedTaskStartedText));
        RaisePropertyChanged(nameof(SelectedTaskCompletedText));
        RaisePropertyChanged(nameof(SelectedTaskCountsText));
        RaisePropertyChanged(nameof(SelectedTaskPendingText));
        RaisePropertyChanged(nameof(SelectedTaskAccentResourceKey));
        RaisePropertyChanged(nameof(SelectedTaskFailureDigestText));
        RaisePropertyChanged(nameof(SelectedTaskJumpEntryText));
    }

    private void RaiseResultSummaryProperties()
    {
        RaiseSelectionProperties();
        RaisePropertyChanged(nameof(SelectedTaskBasicSummary));
        RaisePropertyChanged(nameof(SelectedTaskPlaybackSummary));
        RaisePropertyChanged(nameof(SelectedTaskScreenshotSummary));
        RaisePropertyChanged(nameof(SelectedTaskRecheckSummary));
        RaisePropertyChanged(nameof(SelectedTaskClosureSummary));
    }

    private void OnTasksChanged(object? sender, EventArgs e)
    {
        Dispatch(Reload);
    }

    private void OnScopeChanged(object? sender, EventArgs e)
    {
        Dispatch(RebuildScopeInfo);
    }

    private static int ParseHour(string text)
    {
        return int.TryParse(text, out var value)
            ? Math.Clamp(value, 0, 23)
            : 9;
    }

    private static int ParseMinute(string text)
    {
        return int.TryParse(text, out var value)
            ? Math.Clamp(value, 0, 59)
            : 0;
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static OverviewMetric BuildMetric(string label, int value, string unit, string deltaText, string accentResourceKey)
    {
        return new OverviewMetric
        {
            Label = label,
            Value = value.ToString(),
            Unit = unit,
            DeltaText = deltaText,
            AccentResourceKey = accentResourceKey
        };
    }
}
