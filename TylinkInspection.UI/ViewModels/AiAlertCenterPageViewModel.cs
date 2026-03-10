using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class AiAlertCenterPageViewModel : PageViewModelBase
{
    private const string AllWorkflowOption = "全部状态";
    private const string AllSourceOption = "全部来源";
    private const string AllTypeOption = "全部类型";
    private const string AllTimeOption = "全部时间";
    private const string Last6HoursOption = "近6小时";
    private const string TodayOption = "今日";
    private const string Last24HoursOption = "近24小时";

    private static readonly IReadOnlyDictionary<string, int?> AlertSourceMap = new Dictionary<string, int?>
    {
        [AllSourceOption] = null,
        ["端侧"] = 1,
        ["云化"] = 2,
        ["云测-AI能力中台"] = 3,
        ["平安慧眼"] = 4
    };

    private readonly IAiAlertService _aiAlertService;
    private readonly IDeviceAlarmService _deviceAlarmService;
    private readonly Dictionary<string, int> _alertTypeMap = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadedAlertIds = new(StringComparer.OrdinalIgnoreCase);

    private AiAlertListItem? _selectedAlert;
    private AiAlertDetail? _selectedAlertDetail;
    private string? _deviceCode;
    private string _selectedWorkflow = AllWorkflowOption;
    private string _selectedAlertSource = AllSourceOption;
    private string _selectedAlertType = AllTypeOption;
    private string _selectedTimeRange = Last24HoursOption;
    private string _reviewNote = string.Empty;
    private string _listStatusText = "尚未查询。";
    private string _listErrorText = string.Empty;
    private string _relatedAlarmStatusText = "请选择一条 AI 告警查看关联设备告警。";
    private string _relatedAlarmErrorText = string.Empty;
    private bool _isBusy;
    private bool _hasMoreAlerts = true;
    private int _nextPageNo = 1;
    private DateTimeOffset? _lastSeenTime;
    private string? _lastSeenId;

    public AiAlertCenterPageViewModel(IAiAlertService aiAlertService, IDeviceAlarmService deviceAlarmService)
        : base("AI告警中心", "接入真实 AI 告警列表、筛选、分页续翻和本地状态流转；右侧保留详情与本地复核备注。")
    {
        _aiAlertService = aiAlertService;
        _deviceAlarmService = deviceAlarmService;

        WorkflowOptions =
        [
            AllWorkflowOption,
            AiAlertWorkflowStatus.PendingConfirm,
            AiAlertWorkflowStatus.Confirmed,
            AiAlertWorkflowStatus.Ignored,
            AiAlertWorkflowStatus.Dispatched,
            AiAlertWorkflowStatus.Recovered
        ];

        AlertSourceOptions = new ObservableCollection<string>(AlertSourceMap.Keys);
        AlertTypeOptions = new ObservableCollection<string> { AllTypeOption };
        TimeRangeOptions =
        [
            AllTimeOption,
            Last6HoursOption,
            TodayOption,
            Last24HoursOption
        ];

        SummaryCards = new ObservableCollection<OverviewMetric>();
        AlertItems = new ObservableCollection<AiAlertListItem>();
        RelatedDeviceAlarms = new ObservableCollection<DeviceAlarmListItem>();

        SearchCommand = new RelayCommand<object?>(_ => _ = RefreshAsync());
        RefreshCommand = new RelayCommand<object?>(_ => _ = RefreshAsync());
        LoadMoreCommand = new RelayCommand<object?>(_ => _ = LoadMoreAsync());
        ClearFilterCommand = new RelayCommand<object?>(_ => ClearFilters());
        UpdateWorkflowCommand = new RelayCommand<string>(status => UpdateWorkflow(status));

        _ = RefreshAsync();
    }

    public IReadOnlyList<string> WorkflowOptions { get; }

    public ObservableCollection<string> AlertSourceOptions { get; }

    public ObservableCollection<string> AlertTypeOptions { get; }

    public IReadOnlyList<string> TimeRangeOptions { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<AiAlertListItem> AlertItems { get; }

    public ObservableCollection<DeviceAlarmListItem> RelatedDeviceAlarms { get; }

    public string? DeviceCode
    {
        get => _deviceCode;
        set => SetProperty(ref _deviceCode, value);
    }

    public string SelectedWorkflow
    {
        get => _selectedWorkflow;
        set => SetProperty(ref _selectedWorkflow, value);
    }

    public string SelectedAlertSource
    {
        get => _selectedAlertSource;
        set => SetProperty(ref _selectedAlertSource, value);
    }

    public string SelectedAlertType
    {
        get => _selectedAlertType;
        set => SetProperty(ref _selectedAlertType, value);
    }

    public string SelectedTimeRange
    {
        get => _selectedTimeRange;
        set => SetProperty(ref _selectedTimeRange, value);
    }

    public string ReviewNote
    {
        get => _reviewNote;
        set => SetProperty(ref _reviewNote, value);
    }

    public AiAlertListItem? SelectedAlert
    {
        get => _selectedAlert;
        set
        {
            if (SetProperty(ref _selectedAlert, value))
            {
                _ = LoadSelectedAlertDetailAsync();
            }
        }
    }

    public AiAlertDetail? SelectedAlertDetail
    {
        get => _selectedAlertDetail;
        private set
        {
            if (SetProperty(ref _selectedAlertDetail, value))
            {
                RaiseSelectionProperties();
            }
        }
    }

    public string ListStatusText
    {
        get => _listStatusText;
        private set => SetProperty(ref _listStatusText, value);
    }

    public string ListErrorText
    {
        get => _listErrorText;
        private set => SetProperty(ref _listErrorText, value);
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool HasMoreAlerts
    {
        get => _hasMoreAlerts;
        private set
        {
            if (SetProperty(ref _hasMoreAlerts, value))
            {
                RaisePropertyChanged(nameof(LoadMoreButtonText));
            }
        }
    }

    public string LoadMoreButtonText => HasMoreAlerts ? "加载更多" : "没有更多数据";

    public string SelectedAlertAccentResourceKey => SelectedAlert?.AccentResourceKey ?? "ToneWarningBrush";

    public string SelectedAlertWorkflowText => SelectedAlertDetail?.WorkflowStatus ?? "未选择告警";

    public string SelectedAlertTimeText
    {
        get
        {
            if (SelectedAlertDetail is null)
            {
                return "请选择左侧告警查看时间信息。";
            }

            return $"告警时间 {SelectedAlertDetail.CreateTime:MM-dd HH:mm:ss} / 更新时间 {FormatTime(SelectedAlertDetail.UpdateTime)}";
        }
    }

    public string SelectedAlertPlatformStatusText => SelectedAlertDetail?.PlatformStatusText ?? "--";

    public string SelectedAlertSummaryText => SelectedAlertDetail?.Summary ?? "暂无摘要。";

    public ICommand SearchCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand ClearFilterCommand { get; }

    public ICommand UpdateWorkflowCommand { get; }

    private async Task RefreshAsync()
    {
        ResetPagingState();
        AlertItems.Clear();
        _loadedAlertIds.Clear();
        ListErrorText = string.Empty;
        await LoadNextPageAsync(resetSelection: true);
    }

    private async Task LoadMoreAsync()
    {
        if (IsBusy || !HasMoreAlerts)
        {
            return;
        }

        await LoadNextPageAsync(resetSelection: false);
    }

    private async Task LoadNextPageAsync(bool resetSelection)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var query = BuildQuery();
            var result = await Task.Run(() => _aiAlertService.Query(query));
            MergeAlertTypeOptions(result.Items);
            AppendVisibleItems(result.Items);

            _nextPageNo = result.PageNo + 1;
            _lastSeenTime = result.LastSeenTime;
            _lastSeenId = result.LastSeenId;
            HasMoreAlerts = result.HasMore;

            RebuildSummary(AlertItems.ToList());
            ListStatusText = BuildListStatusText(result);
            ListErrorText = string.Empty;

            var selectedId = resetSelection ? null : SelectedAlert?.Id;
            SelectedAlert = AlertItems.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                ?? AlertItems.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ListErrorText = BuildErrorText(ex);
            ListStatusText = AlertItems.Count == 0 ? "查询失败。" : "列表已保留当前缓存结果。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AiAlertQuery BuildQuery()
    {
        var (startTime, endTime) = ResolveTimeWindow(SelectedTimeRange);
        return new AiAlertQuery
        {
            DeviceCode = string.IsNullOrWhiteSpace(DeviceCode) ? null : DeviceCode.Trim(),
            AlertSource = AlertSourceMap[SelectedAlertSource],
            AlertTypes = SelectedAlertType == AllTypeOption || !_alertTypeMap.TryGetValue(SelectedAlertType, out var alertType)
                ? Array.Empty<int>()
                : new[] { alertType },
            StartTime = startTime,
            EndTime = endTime,
            PageNo = _nextPageNo,
            PageSize = 20,
            LastSeenTime = _lastSeenTime,
            LastSeenId = _lastSeenId
        };
    }

    private void ResetPagingState()
    {
        _nextPageNo = 1;
        _lastSeenTime = null;
        _lastSeenId = null;
        HasMoreAlerts = true;
        ListStatusText = "正在查询...";
        RelatedAlarmErrorText = string.Empty;
        RelatedAlarmStatusText = "请选择一条 AI 告警查看关联设备告警。";
    }

    private void ClearFilters()
    {
        DeviceCode = string.Empty;
        SelectedWorkflow = AllWorkflowOption;
        SelectedAlertSource = AllSourceOption;
        SelectedAlertType = AllTypeOption;
        SelectedTimeRange = Last24HoursOption;
        _ = RefreshAsync();
    }

    private void MergeAlertTypeOptions(IEnumerable<AiAlertListItem> items)
    {
        foreach (var item in items.OrderBy(item => item.AlertTypeName, StringComparer.Ordinal))
        {
            if (_alertTypeMap.ContainsKey(item.AlertTypeName))
            {
                continue;
            }

            _alertTypeMap[item.AlertTypeName] = item.AlertType;
            AlertTypeOptions.Add(item.AlertTypeName);
        }
    }

    private void AppendVisibleItems(IEnumerable<AiAlertListItem> items)
    {
        foreach (var item in items.Where(item => SelectedWorkflow == AllWorkflowOption || item.WorkflowStatus == SelectedWorkflow))
        {
            if (_loadedAlertIds.Add(item.Id))
            {
                AlertItems.Add(item);
            }
        }
    }

    private async Task LoadSelectedAlertDetailAsync()
    {
        if (SelectedAlert is null)
        {
            SelectedAlertDetail = null;
            ReviewNote = string.Empty;
            RelatedDeviceAlarms.Clear();
            RelatedAlarmStatusText = "请选择一条 AI 告警查看关联设备告警。";
            RelatedAlarmErrorText = string.Empty;
            RaiseSelectionProperties();
            return;
        }

        SelectedAlertDetail = _aiAlertService.GetDetail(SelectedAlert.Id);
        ReviewNote = SelectedAlertDetail?.ReviewNote ?? string.Empty;
        await LoadRelatedDeviceAlarmsAsync();
    }

    private async Task LoadRelatedDeviceAlarmsAsync()
    {
        RelatedDeviceAlarms.Clear();
        RelatedAlarmErrorText = string.Empty;

        if (SelectedAlertDetail is null)
        {
            RelatedAlarmStatusText = "未找到本地详情缓存。";
            return;
        }

        try
        {
            var result = await Task.Run(() => _deviceAlarmService.Query(new DeviceAlarmQuery
            {
                DeviceCode = SelectedAlertDetail.DeviceCode,
                StartTime = SelectedAlertDetail.CreateTime.AddDays(-1),
                EndTime = DateTimeOffset.Now,
                PageNo = 1,
                PageSize = 10
            }));

            foreach (var deviceAlarm in result.Items)
            {
                RelatedDeviceAlarms.Add(deviceAlarm);
            }

            RelatedAlarmStatusText = RelatedDeviceAlarms.Count == 0
                ? "当前设备在选定时间窗口内没有普通设备告警。"
                : $"已加载 {RelatedDeviceAlarms.Count} 条关联设备告警。";
        }
        catch (Exception ex)
        {
            RelatedAlarmErrorText = BuildErrorText(ex);
            RelatedAlarmStatusText = "关联设备告警查询失败。";
        }
    }

    private void UpdateWorkflow(string? workflowStatus)
    {
        if (SelectedAlert is null || string.IsNullOrWhiteSpace(workflowStatus))
        {
            return;
        }

        try
        {
            _aiAlertService.UpdateWorkflowStatus(
                SelectedAlert.Id,
                workflowStatus,
                string.IsNullOrWhiteSpace(ReviewNote) ? null : ReviewNote.Trim());

            var detail = _aiAlertService.GetDetail(SelectedAlert.Id);
            SelectedAlertDetail = detail;
            ReviewNote = detail?.ReviewNote ?? string.Empty;

            var index = AlertItems.IndexOf(SelectedAlert);
            if (index >= 0 && detail is not null)
            {
                var updatedItem = new AiAlertListItem
                {
                    Id = detail.Id,
                    MsgId = detail.MsgId,
                    DeviceCode = detail.DeviceCode,
                    DeviceName = detail.DeviceName,
                    AlertType = detail.AlertType,
                    AlertTypeName = detail.AlertTypeName,
                    AlertSource = detail.AlertSource,
                    AlertSourceName = detail.AlertSourceName,
                    Content = detail.Content,
                    CreateTime = detail.CreateTime,
                    UpdateTime = detail.UpdateTime,
                    PlatformStatus = detail.PlatformStatus,
                    PlatformStatusText = detail.PlatformStatusText,
                    Summary = detail.Summary,
                    WorkflowStatus = detail.WorkflowStatus,
                    AccentResourceKey = MapAccent(detail.WorkflowStatus)
                };

                AlertItems[index] = updatedItem;
                SelectedAlert = updatedItem;
            }

            RebuildSummary(AlertItems.ToList());
            ListErrorText = string.Empty;
        }
        catch (Exception ex)
        {
            ListErrorText = BuildErrorText(ex);
        }
    }

    private void RebuildSummary(IReadOnlyList<AiAlertListItem> items)
    {
        var summaryCards = new[]
        {
            BuildSummary("待确认", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.PendingConfirm), "ToneWarningBrush"),
            BuildSummary("已确认", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.Confirmed), "TonePrimaryBrush"),
            BuildSummary("已派单", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.Dispatched), "ToneDangerBrush"),
            BuildSummary("已恢复", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.Recovered), "ToneSuccessBrush")
        };

        SummaryCards.Clear();
        foreach (var item in summaryCards)
        {
            SummaryCards.Add(item);
        }
    }

    private void RaiseSelectionProperties()
    {
        RaisePropertyChanged(nameof(SelectedAlertAccentResourceKey));
        RaisePropertyChanged(nameof(SelectedAlertWorkflowText));
        RaisePropertyChanged(nameof(SelectedAlertTimeText));
        RaisePropertyChanged(nameof(SelectedAlertPlatformStatusText));
        RaisePropertyChanged(nameof(SelectedAlertSummaryText));
    }

    private static OverviewMetric BuildSummary(string label, int value, string accentResourceKey)
    {
        return new OverviewMetric
        {
            Label = label,
            Value = value.ToString(),
            Unit = "条",
            DeltaText = "本地处理状态",
            AccentResourceKey = accentResourceKey
        };
    }

    private static string BuildListStatusText(ScrollQueryResult<AiAlertListItem> result)
    {
        var totalText = result.TotalCount.HasValue ? $" / 平台总数 {result.TotalCount.Value}" : string.Empty;
        return $"当前页 {result.PageNo}，本次加载 {result.Items.Count} 条{totalText}";
    }

    private static string BuildErrorText(Exception exception)
    {
        if (exception is PlatformServiceException platformException)
        {
            var categoryText = platformException.Category switch
            {
                PlatformErrorCategory.Token => "Token 错误",
                PlatformErrorCategory.Parameter => "参数或配置错误",
                PlatformErrorCategory.Platform => "平台返回错误",
                _ => "未知错误"
            };

            return string.IsNullOrWhiteSpace(platformException.ErrorCode)
                ? $"{categoryText}: {platformException.Message}"
                : $"{categoryText} [{platformException.ErrorCode}]: {platformException.Message}";
        }

        return $"未知错误: {exception.Message}";
    }

    private static string MapAccent(string workflowStatus)
    {
        return workflowStatus switch
        {
            AiAlertWorkflowStatus.PendingConfirm => "ToneWarningBrush",
            AiAlertWorkflowStatus.Confirmed => "TonePrimaryBrush",
            AiAlertWorkflowStatus.Ignored => "ToneFocusBrush",
            AiAlertWorkflowStatus.Dispatched => "ToneDangerBrush",
            AiAlertWorkflowStatus.Recovered => "ToneSuccessBrush",
            _ => "ToneWarningBrush"
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
        return value?.ToString("MM-dd HH:mm:ss") ?? "--";
    }
}
