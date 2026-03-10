using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class AiInspectionCenterPageViewModel : PageViewModelBase
{
    private const string AllStatusOption = "全部状态";
    private const string AllTimeOption = "全部时间";
    private const string Last6HoursOption = "近6小时";
    private const string TodayOption = "今日";
    private const string Last24HoursOption = "近24小时";

    private readonly IAiInspectionCenterService _service;
    private readonly IDeviceAlarmService _deviceAlarmService;

    private AiInspectionTaskListItem? _selectedTask;
    private AiInspectionTaskDetail? _selectedTaskDetail;
    private string? _keyword;
    private string? _deviceCode;
    private string _selectedStatus = AllStatusOption;
    private string _selectedTimeRange = Last24HoursOption;
    private string _actionNote = string.Empty;
    private string _relatedAlarmStatusText = "请选择一条巡检任务查看关联设备告警。";
    private string _relatedAlarmErrorText = string.Empty;

    public AiInspectionCenterPageViewModel(IAiInspectionCenterService service, IDeviceAlarmService deviceAlarmService)
        : base("AI智能巡检中心", "保留巡检任务筛选、状态流转和执行记录，同时增加关联普通设备告警联动区域。")
    {
        _service = service;
        _deviceAlarmService = deviceAlarmService;

        StatusOptions =
        [
            AllStatusOption,
            AiInspectionTaskStatus.Pending,
            AiInspectionTaskStatus.Running,
            AiInspectionTaskStatus.Completed,
            AiInspectionTaskStatus.Faulted
        ];

        TimeRangeOptions =
        [
            AllTimeOption,
            Last6HoursOption,
            TodayOption,
            Last24HoursOption
        ];

        SummaryCards = new ObservableCollection<OverviewMetric>();
        TaskItems = new ObservableCollection<AiInspectionTaskListItem>();
        ExecutionRecords = new ObservableCollection<AiInspectionExecutionRecord>();
        RelatedDeviceAlarms = new ObservableCollection<DeviceAlarmListItem>();

        SearchCommand = new RelayCommand<object?>(_ => Reload());
        ClearFilterCommand = new RelayCommand<object?>(_ => ClearFilters());
        UpdateStatusCommand = new RelayCommand<string>(status => UpdateStatus(status));
        RetryCommand = new RelayCommand<object?>(_ => RetrySelectedTask());

        Reload();
    }

    public IReadOnlyList<string> StatusOptions { get; }

    public IReadOnlyList<string> TimeRangeOptions { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<AiInspectionTaskListItem> TaskItems { get; }

    public ObservableCollection<AiInspectionExecutionRecord> ExecutionRecords { get; }

    public ObservableCollection<DeviceAlarmListItem> RelatedDeviceAlarms { get; }

    public string? Keyword
    {
        get => _keyword;
        set => SetProperty(ref _keyword, value);
    }

    public string? DeviceCode
    {
        get => _deviceCode;
        set => SetProperty(ref _deviceCode, value);
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string SelectedTimeRange
    {
        get => _selectedTimeRange;
        set => SetProperty(ref _selectedTimeRange, value);
    }

    public string ActionNote
    {
        get => _actionNote;
        set => SetProperty(ref _actionNote, value);
    }

    public string RelatedAlarmStatusText
    {
        get => _relatedAlarmStatusText;
        private set => SetProperty(ref _relatedAlarmStatusText, value);
    }

    public string RelatedAlarmErrorText
    {
        get => _relatedAlarmErrorText;
        private set => SetProperty(ref _relatedAlarmErrorText, value);
    }

    public AiInspectionTaskListItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                _ = LoadSelectedTaskDetailAsync();
            }
        }
    }

    public AiInspectionTaskDetail? SelectedTaskDetail
    {
        get => _selectedTaskDetail;
        private set
        {
            if (SetProperty(ref _selectedTaskDetail, value))
            {
                RaiseSelectionProperties();
            }
        }
    }

    public string SelectedTaskAccentResourceKey => SelectedTask?.AccentResourceKey ?? "TonePrimaryBrush";

    public string SelectedTaskStatusText => SelectedTaskDetail?.Status ?? "未选择任务";

    public string SelectedTaskWindowText
    {
        get
        {
            if (SelectedTaskDetail is null)
            {
                return "请选择左侧任务查看执行窗口。";
            }

            return $"计划 {SelectedTaskDetail.ScheduledAt:MM-dd HH:mm} / 启动 {FormatTime(SelectedTaskDetail.StartedAt)} / 完成 {FormatTime(SelectedTaskDetail.FinishedAt)}";
        }
    }

    public string SelectedTaskLatestNoteText => string.IsNullOrWhiteSpace(SelectedTaskDetail?.LatestNote) ? "暂无备注" : SelectedTaskDetail.LatestNote!;

    public string SelectedTaskDescriptionText => SelectedTaskDetail?.Description ?? "暂无任务说明。";

    public string SelectedTaskStrategyText => SelectedTaskDetail?.StrategyName ?? "未绑定策略";

    public ICommand SearchCommand { get; }

    public ICommand ClearFilterCommand { get; }

    public ICommand UpdateStatusCommand { get; }

    public ICommand RetryCommand { get; }

    private void Reload()
    {
        var (startTime, endTime) = ResolveTimeWindow(SelectedTimeRange);
        var query = new AiInspectionTaskQuery
        {
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(),
            DeviceCode = string.IsNullOrWhiteSpace(DeviceCode) ? null : DeviceCode.Trim(),
            Status = SelectedStatus == AllStatusOption ? null : SelectedStatus,
            StartTime = startTime,
            EndTime = endTime
        };

        var items = _service.Query(query).ToList();

        TaskItems.Clear();
        foreach (var item in items)
        {
            TaskItems.Add(item);
        }

        RebuildSummary(items);

        var selectedTaskId = SelectedTask?.TaskId;
        SelectedTask = items.FirstOrDefault(item => item.TaskId == selectedTaskId) ?? items.FirstOrDefault();
    }

    private void ClearFilters()
    {
        Keyword = string.Empty;
        DeviceCode = string.Empty;
        SelectedStatus = AllStatusOption;
        SelectedTimeRange = Last24HoursOption;
        Reload();
    }

    private async Task LoadSelectedTaskDetailAsync()
    {
        if (SelectedTask is null)
        {
            SelectedTaskDetail = null;
            ExecutionRecords.Clear();
            RelatedDeviceAlarms.Clear();
            RelatedAlarmStatusText = "请选择一条巡检任务查看关联设备告警。";
            RelatedAlarmErrorText = string.Empty;
            RaiseSelectionProperties();
            return;
        }

        SelectedTaskDetail = _service.GetDetail(SelectedTask.TaskId);

        ExecutionRecords.Clear();
        var executionRecords = SelectedTaskDetail?.ExecutionRecords.OrderByDescending(item => item.Timestamp)
            ?? Enumerable.Empty<AiInspectionExecutionRecord>();

        foreach (var record in executionRecords)
        {
            ExecutionRecords.Add(record);
        }

        await LoadRelatedDeviceAlarmsAsync();
    }

    private async Task LoadRelatedDeviceAlarmsAsync()
    {
        RelatedDeviceAlarms.Clear();
        RelatedAlarmErrorText = string.Empty;

        if (SelectedTaskDetail is null)
        {
            RelatedAlarmStatusText = "未找到任务详情。";
            return;
        }

        try
        {
            var result = await Task.Run(() => _deviceAlarmService.Query(new DeviceAlarmQuery
            {
                DeviceCode = SelectedTaskDetail.DeviceCode,
                StartTime = SelectedTaskDetail.ScheduledAt.AddDays(-1),
                EndTime = DateTimeOffset.Now,
                PageNo = 1,
                PageSize = 10
            }));

            foreach (var item in result.Items)
            {
                RelatedDeviceAlarms.Add(item);
            }

            RelatedAlarmStatusText = RelatedDeviceAlarms.Count == 0
                ? "当前设备在任务时间窗口内没有普通设备告警。"
                : $"已加载 {RelatedDeviceAlarms.Count} 条关联设备告警。";
        }
        catch (Exception ex)
        {
            RelatedAlarmStatusText = "关联设备告警查询失败。";
            RelatedAlarmErrorText = ex is PlatformServiceException platformException
                ? $"{platformException.Category} [{platformException.ErrorCode ?? "--"}]: {platformException.Message}"
                : ex.Message;
        }
    }

    private void UpdateStatus(string? status)
    {
        if (SelectedTask is null || string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        _service.UpdateStatus(new AiInspectionTaskMutation
        {
            TaskId = SelectedTask.TaskId,
            TargetStatus = status,
            Note = string.IsNullOrWhiteSpace(ActionNote) ? null : ActionNote.Trim()
        });

        ActionNote = string.Empty;
        Reload();
    }

    private void RetrySelectedTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        _service.RetryTask(
            SelectedTask.TaskId,
            string.IsNullOrWhiteSpace(ActionNote) ? "手动重试任务" : ActionNote.Trim());

        ActionNote = string.Empty;
        Reload();
    }

    private void RebuildSummary(IReadOnlyList<AiInspectionTaskListItem> items)
    {
        var summaryCards = new[]
        {
            BuildSummary("待执行", items.Count(item => item.Status == AiInspectionTaskStatus.Pending), "TonePrimaryBrush"),
            BuildSummary("执行中", items.Count(item => item.Status == AiInspectionTaskStatus.Running), "ToneInfoBrush"),
            BuildSummary("已完成", items.Count(item => item.Status == AiInspectionTaskStatus.Completed), "ToneSuccessBrush"),
            BuildSummary("异常中", items.Count(item => item.Status == AiInspectionTaskStatus.Faulted), "ToneDangerBrush")
        };

        SummaryCards.Clear();
        foreach (var item in summaryCards)
        {
            SummaryCards.Add(item);
        }
    }

    private void RaiseSelectionProperties()
    {
        RaisePropertyChanged(nameof(SelectedTaskAccentResourceKey));
        RaisePropertyChanged(nameof(SelectedTaskStatusText));
        RaisePropertyChanged(nameof(SelectedTaskWindowText));
        RaisePropertyChanged(nameof(SelectedTaskLatestNoteText));
        RaisePropertyChanged(nameof(SelectedTaskDescriptionText));
        RaisePropertyChanged(nameof(SelectedTaskStrategyText));
    }

    private static OverviewMetric BuildSummary(string label, int value, string accentResourceKey)
    {
        return new OverviewMetric
        {
            Label = label,
            Value = value.ToString(),
            Unit = "项",
            DeltaText = "本地状态流转",
            AccentResourceKey = accentResourceKey
        };
    }

    private static (DateTimeOffset? StartTime, DateTimeOffset? EndTime) ResolveTimeWindow(string selectedOption)
    {
        var now = DateTimeOffset.Now;

        return selectedOption switch
        {
            Last6HoursOption => (now.AddHours(-6), now),
            TodayOption => (new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset), now),
            Last24HoursOption => (now.AddHours(-24), now),
            _ => (null, null)
        };
    }

    private static string FormatTime(DateTimeOffset? value)
    {
        return value?.ToString("MM-dd HH:mm") ?? "--";
    }
}
