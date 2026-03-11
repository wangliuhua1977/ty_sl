using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class AiAlertCenterPageViewModel : PageViewModelBase
{
    private const int FocusedAlertType = 3;
    private const string FocusedAlertTypeName = "\u753b\u9762\u5f02\u5e38\u5de1\u68c0";
    private const string AllWorkflowOption = "\u5168\u90e8\u72b6\u6001";
    private const string AllSourceOption = "\u5168\u90e8\u6765\u6e90";
    private const string AllTimeOption = "\u5168\u90e8\u65f6\u95f4";
    private const string Last6HoursOption = "\u8fd16\u5c0f\u65f6";
    private const string TodayOption = "\u4eca\u65e5";
    private const string Last24HoursOption = "\u8fd124\u5c0f\u65f6";
    private const int AlertPageSize = 20;
    private const int RelatedAlarmPageSize = 10;

    private static readonly IReadOnlyDictionary<string, int?> AlertSourceMap = new Dictionary<string, int?>
    {
        [AllSourceOption] = null,
        ["\u7aef\u4fa7"] = 1,
        ["\u4e91\u5316"] = 2,
        ["\u4e91\u6d4b-AI\u80fd\u529b\u4e2d\u53f0"] = 3,
        ["\u5e73\u5b89\u6167\u773c"] = 4
    };

    private readonly IAiAlertService _aiAlertService;
    private readonly IDeviceAlarmService _deviceAlarmService;
    private readonly Dictionary<string, int> _alertTypeMap = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadedAlertIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedRelatedAlarmIds = new(StringComparer.OrdinalIgnoreCase);

    private AiAlertListItem? _selectedAlert;
    private AiAlertDetail? _selectedAlertDetail;
    private string? _deviceCode;
    private string _selectedWorkflow = AllWorkflowOption;
    private string _selectedAlertSource = AllSourceOption;
    private string _selectedTimeRange = Last24HoursOption;
    private string _reviewNote = string.Empty;
    private string _listStatusText = "\u5f53\u524d\u4ec5\u63a5\u5165 AI \u753b\u9762\u5f02\u5e38\u5de1\u68c0\u544a\u8b66\u3002";
    private string _listErrorText = string.Empty;
    private string _relatedAlarmStatusText = "\u8bf7\u9009\u62e9\u4e00\u6761\u753b\u9762\u5f02\u5e38\u5de1\u68c0\u544a\u8b66\u67e5\u770b\u5173\u8054\u8bbe\u5907\u544a\u8b66\u3002";
    private string _relatedAlarmErrorText = string.Empty;
    private bool _isBusy;
    private bool _hasMoreAlerts = true;
    private bool _hasMoreRelatedAlarms;
    private int _nextPageNo = 1;
    private int _nextRelatedAlarmPageNo = 1;
    private DateTimeOffset? _lastSeenTime;
    private DateTimeOffset? _lastRelatedAlarmSeenTime;
    private string? _lastSeenId;
    private string? _lastRelatedAlarmSeenId;

    public AiAlertCenterPageViewModel(IAiAlertService aiAlertService, IDeviceAlarmService deviceAlarmService)
        : base(
            "AI\u544a\u8b66\u4e2d\u5fc3",
            "\u5f53\u524d\u63a5\u5165 AI \u753b\u9762\u5f02\u5e38\u5de1\u68c0\u544a\u8b66\uff0c\u4ec5\u5c55\u793a\u4e0e\u6162\u76f4\u64ad\u8fd0\u7ef4\u5de1\u68c0\u76f8\u5173\u7684\u753b\u9762\u5f02\u5e38\u6d88\u606f\u3002\u540e\u7eed\u5982\u9700\u6269\u5c55\u5176\u4ed6 AI \u7c7b\u578b\uff0c\u518d\u6309\u6a21\u5757\u9010\u6b65\u63a5\u5165\u3002")
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
        _alertTypeMap[FocusedAlertTypeName] = FocusedAlertType;
        AlertTypeOptions = new ObservableCollection<string> { FocusedAlertTypeName };
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
        LoadMoreRelatedAlarmsCommand = new RelayCommand<object?>(_ => _ = LoadMoreRelatedAlarmsAsync());
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

    public bool HasMoreRelatedAlarms
    {
        get => _hasMoreRelatedAlarms;
        private set
        {
            if (SetProperty(ref _hasMoreRelatedAlarms, value))
            {
                RaisePropertyChanged(nameof(LoadMoreRelatedAlarmsButtonText));
            }
        }
    }

    public string LoadMoreButtonText => HasMoreAlerts ? "\u52a0\u8f7d\u66f4\u591a" : "\u6ca1\u6709\u66f4\u591a\u6570\u636e";

    public string LoadMoreRelatedAlarmsButtonText => HasMoreRelatedAlarms ? "\u52a0\u8f7d\u66f4\u591a\u5173\u8054\u544a\u8b66" : "\u5173\u8054\u544a\u8b66\u5df2\u52a0\u8f7d\u5b8c";

    public string SelectedAlertAccentResourceKey => SelectedAlert?.AccentResourceKey ?? "ToneWarningBrush";

    public string SelectedAlertWorkflowText => SelectedAlertDetail?.WorkflowStatus ?? "\u672a\u9009\u62e9\u544a\u8b66";

    public string SelectedAlertTimeText
    {
        get
        {
            if (SelectedAlertDetail is null)
            {
                return "\u8bf7\u9009\u62e9\u5de6\u4fa7\u544a\u8b66\u67e5\u770b\u65f6\u95f4\u4fe1\u606f\u3002";
            }

            return $"\u544a\u8b66\u65f6\u95f4 {SelectedAlertDetail.CreateTime:MM-dd HH:mm:ss} / \u66f4\u65b0\u65f6\u95f4 {FormatTime(SelectedAlertDetail.UpdateTime)}";
        }
    }

    public string SelectedAlertPlatformStatusText => SelectedAlertDetail?.PlatformStatusText ?? "--";

    public string SelectedAlertSummaryText => SelectedAlertDetail?.Summary ?? "\u6682\u65e0\u6458\u8981\u3002";

    public ICommand SearchCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand LoadMoreRelatedAlarmsCommand { get; }

    public ICommand ClearFilterCommand { get; }

    public ICommand UpdateWorkflowCommand { get; }

    private async Task RefreshAsync()
    {
        ResetAlertPagingState();
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
            AppendVisibleItems(result.Items);

            _nextPageNo = result.PageNo + 1;
            _lastSeenTime = result.LastSeenTime;
            _lastSeenId = result.LastSeenId;
            HasMoreAlerts = result.HasMore;

            RebuildSummary(AlertItems.ToList());
            ListStatusText = BuildListStatusText(result, AlertItems.Count);
            ListErrorText = string.Empty;

            var selectedId = resetSelection ? null : SelectedAlert?.Id;
            SelectedAlert = AlertItems.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                ?? AlertItems.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ListErrorText = BuildErrorText(ex);
            ListStatusText = AlertItems.Count == 0 ? "\u67e5\u8be2\u5931\u8d25\u3002" : "\u5217\u8868\u5df2\u4fdd\u7559\u5f53\u524d\u7f13\u5b58\u7ed3\u679c\u3002";
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
            AlertTypes = new[] { FocusedAlertType },
            StartTime = startTime,
            EndTime = endTime,
            PageNo = _nextPageNo,
            PageSize = AlertPageSize,
            LastSeenTime = _lastSeenTime,
            LastSeenId = _lastSeenId
        };
    }

    private void ResetAlertPagingState()
    {
        _nextPageNo = 1;
        _lastSeenTime = null;
        _lastSeenId = null;
        HasMoreAlerts = true;
        ListStatusText = "\u6b63\u5728\u67e5\u8be2...";
        ResetRelatedAlarmState(clearItems: true);
    }

    private void ResetRelatedAlarmState(bool clearItems)
    {
        _nextRelatedAlarmPageNo = 1;
        _lastRelatedAlarmSeenTime = null;
        _lastRelatedAlarmSeenId = null;
        _loadedRelatedAlarmIds.Clear();
        HasMoreRelatedAlarms = false;
        RelatedAlarmErrorText = string.Empty;
        RelatedAlarmStatusText = "\u8bf7\u9009\u62e9\u4e00\u6761\u753b\u9762\u5f02\u5e38\u5de1\u68c0\u544a\u8b66\u67e5\u770b\u5173\u8054\u8bbe\u5907\u544a\u8b66\u3002";

        if (clearItems)
        {
            RelatedDeviceAlarms.Clear();
        }
    }

    private void ClearFilters()
    {
        DeviceCode = string.Empty;
        SelectedWorkflow = AllWorkflowOption;
        SelectedAlertSource = AllSourceOption;
        SelectedTimeRange = Last24HoursOption;
        _ = RefreshAsync();
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
            ResetRelatedAlarmState(clearItems: true);
            RaiseSelectionProperties();
            return;
        }

        SelectedAlertDetail = _aiAlertService.GetDetail(SelectedAlert.Id);
        ReviewNote = SelectedAlertDetail?.ReviewNote ?? string.Empty;
        await LoadRelatedDeviceAlarmsAsync(reset: true);
    }

    private async Task LoadMoreRelatedAlarmsAsync()
    {
        if (!HasMoreRelatedAlarms || SelectedAlertDetail is null)
        {
            return;
        }

        await LoadRelatedDeviceAlarmsAsync(reset: false);
    }

    private async Task LoadRelatedDeviceAlarmsAsync(bool reset)
    {
        if (SelectedAlertDetail is null)
        {
            RelatedAlarmStatusText = "\u672a\u627e\u5230\u672c\u5730\u8be6\u60c5\u7f13\u5b58\u3002";
            RelatedAlarmErrorText = string.Empty;
            HasMoreRelatedAlarms = false;
            return;
        }

        if (reset)
        {
            ResetRelatedAlarmState(clearItems: true);
            RelatedAlarmStatusText = "\u6b63\u5728\u52a0\u8f7d\u5173\u8054\u8bbe\u5907\u544a\u8b66...";
        }

        try
        {
            var result = await Task.Run(() => _deviceAlarmService.Query(BuildRelatedAlarmQuery()));

            foreach (var deviceAlarm in result.Items)
            {
                if (_loadedRelatedAlarmIds.Add(deviceAlarm.Id))
                {
                    RelatedDeviceAlarms.Add(deviceAlarm);
                }
            }

            _nextRelatedAlarmPageNo = result.PageNo + 1;
            _lastRelatedAlarmSeenTime = result.LastSeenTime;
            _lastRelatedAlarmSeenId = result.LastSeenId;
            HasMoreRelatedAlarms = result.HasMore;
            RelatedAlarmErrorText = string.Empty;
            RelatedAlarmStatusText = RelatedDeviceAlarms.Count == 0
                ? "\u5f53\u524d\u8bbe\u5907\u5728\u9009\u5b9a\u65f6\u95f4\u7a97\u53e3\u5185\u6ca1\u6709\u666e\u901a\u8bbe\u5907\u544a\u8b66\u3002"
                : BuildRelatedAlarmStatusText(result, RelatedDeviceAlarms.Count);
        }
        catch (Exception ex)
        {
            RelatedAlarmErrorText = BuildErrorText(ex);
            RelatedAlarmStatusText = "\u5173\u8054\u8bbe\u5907\u544a\u8b66\u67e5\u8be2\u5931\u8d25\u3002";
            HasMoreRelatedAlarms = false;
        }
    }

    private DeviceAlarmQuery BuildRelatedAlarmQuery()
    {
        return new DeviceAlarmQuery
        {
            DeviceCode = SelectedAlertDetail?.DeviceCode,
            StartTime = SelectedAlertDetail?.CreateTime.AddDays(-1),
            EndTime = DateTimeOffset.Now,
            PageNo = _nextRelatedAlarmPageNo,
            PageSize = RelatedAlarmPageSize,
            LastSeenTime = _lastRelatedAlarmSeenTime,
            LastSeenId = _lastRelatedAlarmSeenId
        };
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
            BuildSummary("\u5f85\u786e\u8ba4", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.PendingConfirm), "ToneWarningBrush"),
            BuildSummary("\u5df2\u786e\u8ba4", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.Confirmed), "TonePrimaryBrush"),
            BuildSummary("\u5df2\u6d3e\u5355", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.Dispatched), "ToneDangerBrush"),
            BuildSummary("\u5df2\u6062\u590d", items.Count(item => item.WorkflowStatus == AiAlertWorkflowStatus.Recovered), "ToneSuccessBrush")
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
            Unit = "\u6761",
            DeltaText = "\u672c\u5730\u5904\u7406\u72b6\u6001",
            AccentResourceKey = accentResourceKey
        };
    }

    private static string BuildListStatusText(ScrollQueryResult<AiAlertListItem> result, int loadedCount)
    {
        var totalText = result.TotalCount.HasValue ? $" / \u5e73\u53f0\u603b\u6570 {result.TotalCount.Value}" : string.Empty;
        return $"\u5f53\u524d\u9875 {result.PageNo}\uff0c\u672c\u6b21\u52a0\u8f7d {result.Items.Count} \u6761\uff0c\u5217\u8868\u7d2f\u79ef {loadedCount} \u6761{totalText}";
    }

    private static string BuildRelatedAlarmStatusText(ScrollQueryResult<DeviceAlarmListItem> result, int loadedCount)
    {
        var totalText = result.TotalCount.HasValue ? $" / \u5e73\u53f0\u603b\u6570 {result.TotalCount.Value}" : string.Empty;
        return $"\u5173\u8054\u544a\u8b66\u5df2\u52a0\u8f7d {loadedCount} \u6761\uff0c\u672c\u6b21 {result.Items.Count} \u6761{totalText}";
    }

    private static string BuildErrorText(Exception exception)
    {
        if (exception is PlatformServiceException platformException)
        {
            var categoryText = platformException.Category switch
            {
                PlatformErrorCategory.Token => "Token \u9519\u8bef",
                PlatformErrorCategory.Parameter => "\u53c2\u6570\u6216\u914d\u7f6e\u9519\u8bef",
                PlatformErrorCategory.Platform => "\u5e73\u53f0\u8fd4\u56de\u9519\u8bef",
                _ => "\u672a\u77e5\u9519\u8bef"
            };

            return string.IsNullOrWhiteSpace(platformException.ErrorCode)
                ? $"{categoryText}: {platformException.Message}"
                : $"{categoryText} [{platformException.ErrorCode}]: {platformException.Message}";
        }

        return $"\u672a\u77e5\u9519\u8bef: {exception.Message}";
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
