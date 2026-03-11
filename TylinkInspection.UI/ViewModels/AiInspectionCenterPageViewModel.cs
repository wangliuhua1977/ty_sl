using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class AiInspectionCenterPageViewModel : PageViewModelBase
{
    private const string AllStatusOption = "全部状态";

    private readonly IAiInspectionTaskService _taskService;
    private readonly IInspectionScopeService _inspectionScopeService;

    private AiInspectionTaskBatch? _selectedTask;
    private string _taskName = string.Empty;
    private string _selectedCreateTaskType = AiInspectionTaskType.BasicInspection;
    private string _selectedCreateScopeMode = AiInspectionTaskScopeMode.FullScheme;
    private string _selectedStatus = AllStatusOption;
    private string _keyword = string.Empty;
    private string _operatorName = Environment.UserName;
    private string _currentSchemeName = "--";
    private string _currentSchemeSummary = "当前未加载巡检范围。";
    private string _selectedTaskProgressText = "--";
    private string _selectedTaskFailureSummary = "暂无失败摘要。";
    private string _selectedTaskLatestSummary = "暂无结果摘要。";

    public AiInspectionCenterPageViewModel(
        IAiInspectionTaskService taskService,
        IInspectionScopeService inspectionScopeService)
        : base("AI智能巡检中心", "围绕批量任务、队列执行、子任务明细与恢复能力，升级为正式任务总控页。")
    {
        _taskService = taskService;
        _inspectionScopeService = inspectionScopeService;

        SummaryCards = new ObservableCollection<OverviewMetric>();
        TaskItems = new ObservableCollection<AiInspectionTaskBatch>();
        DetailItems = new ObservableCollection<AiInspectionTaskItem>();
        ExecutionRecords = new ObservableCollection<AiInspectionTaskExecutionRecord>();

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
        StartSelectedTaskCommand = new RelayCommand<object?>(_ => StartSelectedTask());
        CancelSelectedTaskCommand = new RelayCommand<object?>(_ => CancelSelectedTask());
        RefreshCommand = new RelayCommand<object?>(_ => Reload());
        ClearFilterCommand = new RelayCommand<object?>(_ => ClearFilter());

        _taskService.TasksChanged += OnTasksChanged;
        _inspectionScopeService.ScopeChanged += OnScopeChanged;

        Reload();
    }

    public IReadOnlyList<string> StatusOptions { get; }

    public IReadOnlyList<SelectionItemViewModel> TaskTypeOptions { get; }

    public IReadOnlyList<SelectionItemViewModel> ScopeModeOptions { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<AiInspectionTaskBatch> TaskItems { get; }

    public ObservableCollection<AiInspectionTaskItem> DetailItems { get; }

    public ObservableCollection<AiInspectionTaskExecutionRecord> ExecutionRecords { get; }

    public ICommand CreateAndRunCommand { get; }

    public ICommand StartSelectedTaskCommand { get; }

    public ICommand CancelSelectedTaskCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ClearFilterCommand { get; }

    public string TaskName
    {
        get => _taskName;
        set => SetProperty(ref _taskName, value);
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

    public string SelectedTaskStatusText => SelectedTask?.StatusText ?? "未选择任务";

    public string SelectedTaskTypeText => SelectedTask?.TaskTypeText ?? "--";

    public string SelectedTaskScopeText => SelectedTask?.ScopeModeText ?? "--";

    public string SelectedTaskSchemeText => SelectedTask is null ? "--" : $"{SelectedTask.SchemeName} / {SelectedTask.SchemeId}";

    public string SelectedTaskCreatedText => SelectedTask?.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedTaskStartedText => SelectedTask?.StartedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedTaskCompletedText => SelectedTask?.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedTaskCountsText => SelectedTask is null
        ? "--"
        : $"总数 {SelectedTask.TotalCount} / 成功 {SelectedTask.SucceededCount} / 失败 {SelectedTask.FailedCount} / 异常 {SelectedTask.AbnormalCount}";

    public string SelectedTaskAccentResourceKey => SelectedTask?.AccentResourceKey ?? "TonePrimaryBrush";

    private void Reload()
    {
        var overview = _taskService.GetOverview(BuildQuery());
        var items = _taskService.Query(BuildQuery()).ToList();
        var selectedTaskId = SelectedTask?.TaskId;

        RebuildScopeInfo();
        RebuildSummary(overview);

        TaskItems.Clear();
        foreach (var item in items)
        {
            TaskItems.Add(item);
        }

        SelectedTask = items.FirstOrDefault(item => item.TaskId == selectedTaskId) ?? items.FirstOrDefault();
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
        SummaryCards.Add(BuildMetric("已完成", overview.SucceededTaskCount, "个", "成功批次", "ToneSuccessBrush"));
        SummaryCards.Add(BuildMetric("异常点位", overview.AbnormalItemCount, "个", "批次累计异常", "ToneWarningBrush"));
    }

    private void RebuildDetail()
    {
        DetailItems.Clear();
        ExecutionRecords.Clear();

        if (SelectedTask is null)
        {
            SelectedTaskProgressText = "--";
            SelectedTaskFailureSummary = "暂无失败摘要。";
            SelectedTaskLatestSummary = "暂无结果摘要。";
            return;
        }

        var detail = _taskService.GetDetail(SelectedTask.TaskId) ?? SelectedTask;
        _selectedTask = detail;

        foreach (var item in detail.Items.OrderByDescending(item => item.IsAbnormalResult).ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            DetailItems.Add(item);
        }

        foreach (var record in detail.ExecutionRecords.OrderByDescending(item => item.Timestamp).Take(20))
        {
            ExecutionRecords.Add(record);
        }

        SelectedTaskProgressText = $"{detail.ProgressText} / 成功 {detail.SucceededCount} / 失败 {detail.FailedCount} / 取消 {detail.CanceledCount}";
        SelectedTaskFailureSummary = string.IsNullOrWhiteSpace(detail.FailureSummary) ? "暂无失败摘要。" : detail.FailureSummary;
        SelectedTaskLatestSummary = string.IsNullOrWhiteSpace(detail.LatestResultSummary) ? "暂无结果摘要。" : detail.LatestResultSummary;
    }

    private void CreateAndRunTask()
    {
        try
        {
            _taskService.CreateTask(new AiInspectionTaskCreateRequest
            {
                TaskName = TaskName,
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

    private void ClearFilter()
    {
        Keyword = string.Empty;
        SelectedStatus = AllStatusOption;
        Reload();
    }

    private AiInspectionTaskQuery BuildQuery()
    {
        return new AiInspectionTaskQuery
        {
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(),
            Status = SelectedStatus == AllStatusOption ? null : SelectedStatus
        };
    }

    private void RaiseSelectionProperties()
    {
        RaisePropertyChanged(nameof(SelectedTaskStatusText));
        RaisePropertyChanged(nameof(SelectedTaskTypeText));
        RaisePropertyChanged(nameof(SelectedTaskScopeText));
        RaisePropertyChanged(nameof(SelectedTaskSchemeText));
        RaisePropertyChanged(nameof(SelectedTaskCreatedText));
        RaisePropertyChanged(nameof(SelectedTaskStartedText));
        RaisePropertyChanged(nameof(SelectedTaskCompletedText));
        RaisePropertyChanged(nameof(SelectedTaskCountsText));
        RaisePropertyChanged(nameof(SelectedTaskAccentResourceKey));
    }

    private void OnTasksChanged(object? sender, EventArgs e)
    {
        Dispatch(Reload);
    }

    private void OnScopeChanged(object? sender, EventArgs e)
    {
        Dispatch(RebuildScopeInfo);
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
