using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class MapInspectionPageViewModel : PageViewModelBase
{
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly IManualCoordinateService _manualCoordinateService;
    private readonly IInspectionSelectionService _inspectionSelectionService;
    private readonly DeviceMediaReviewViewModel _mediaReview;

    private InspectionScopeResult? _scopeResult;
    private DeviceInspectionResult? _selectedInspectionResult;
    private SelectionItemViewModel? _selectedScheme;
    private SelectionItemViewModel? _selectedTaskType;
    private string _lastUpdatedText = string.Empty;
    private string _currentSchemeName = "默认巡检范围";
    private string _selectedPointName = "未选中点位";
    private string _selectedPointDeviceCode = "--";
    private string _selectedPointStatusText = "请点击地图点位或待补录列表。";
    private string _selectedPointDirectoryPath = "--";
    private string _selectedCoordinateSourceText = "待补录";
    private string _selectedCoordinateText = "--";
    private string _selectedCoordinateUpdatedText = "--";
    private string _selectedCoordinateRemarkText = "暂无人工备注";
    private string _editorLongitude = string.Empty;
    private string _editorLatitude = string.Empty;
    private string _editorRemark = string.Empty;
    private string _editorStatusText = "支持手工录入 GCJ-02 坐标，也可开启取点模式后单击地图。";
    private string _editorErrorText = string.Empty;
    private string _mapErrorText = string.Empty;
    private string _inspectionStatusText = "请选择点位后执行基础巡检。";
    private string _inspectionAlertText = string.Empty;
    private bool _isPickMode;
    private bool _isInspectingSelectedPoint;
    private bool _suppressScopeEvent;
    private bool _suppressSelectionSync;

    public MapInspectionPageViewModel(
        InspectionWorkspaceData workspace,
        IInspectionScopeService inspectionScopeService,
        IDeviceInspectionService deviceInspectionService,
        IManualCoordinateService manualCoordinateService,
        IInspectionSelectionService inspectionSelectionService,
        IPlaybackReviewService playbackReviewService,
        IScreenshotSamplingService screenshotSamplingService,
        ICloudPlaybackService cloudPlaybackService,
        AmapMapOptions mapOptions)
        : base("地图巡检台", "地图页正式承载当前巡检范围点位、真实高德地图和人工坐标治理，坐标统一按 GCJ-02 处理。")
    {
        _inspectionScopeService = inspectionScopeService;
        _deviceInspectionService = deviceInspectionService;
        _manualCoordinateService = manualCoordinateService;
        _inspectionSelectionService = inspectionSelectionService;
        _mediaReview = new DeviceMediaReviewViewModel(playbackReviewService, screenshotSamplingService, cloudPlaybackService);
        MapOptions = mapOptions;

        _inspectionScopeService.ScopeChanged += OnScopeChanged;
        _inspectionSelectionService.SelectionChanged += OnSelectionChanged;

        SchemeItems = new ObservableCollection<SelectionItemViewModel>();
        TaskTypeItems = new ObservableCollection<SelectionItemViewModel>
        {
            new() { Title = "常规巡检", Subtitle = "按当前方案承载在线、离线与坐标治理点位。", Badge = "SCOPE", IsSelected = true },
            new() { Title = "AI 巡检", Subtitle = "预留后续 AI 巡检、截图复核和告警联动。", Badge = "AI" },
            new() { Title = "复检任务", Subtitle = "预留定时复检、恢复销警和派单闭环。", Badge = "RECHECK" }
        };

        OverviewMetrics = new ObservableCollection<OverviewMetric>();
        AlertItems = new ObservableCollection<AlertItem>(workspace.AlertItems);
        MapPoints = new ObservableCollection<InspectionScopeMapPoint>();
        MissingCoordinateItems = new ObservableCollection<SelectionItemViewModel>();

        CurrentTask = workspace.CurrentTask;
        _selectedTaskType = TaskTypeItems.First(item => item.IsSelected);

        SelectSchemeCommand = new RelayCommand<SelectionItemViewModel>(item => _ = SelectSchemeAsync(item));
        SelectTaskTypeCommand = new RelayCommand<SelectionItemViewModel>(item => SelectSingle(item, TaskTypeItems, value =>
        {
            _selectedTaskType = value;
            RaisePropertyChanged(nameof(MapCaption));
        }));
        SelectMissingCoordinateCommand = new RelayCommand<SelectionItemViewModel>(item =>
        {
            if (item?.Key is not null)
            {
                SelectPoint(item.Key, syncSelection: true);
            }
        });
        TogglePickModeCommand = new RelayCommand<object?>(_ => TogglePickMode());
        SaveManualCoordinateCommand = new RelayCommand<object?>(_ => _ = SaveManualCoordinateAsync());
        ClearManualCoordinateCommand = new RelayCommand<object?>(_ => _ = ClearManualCoordinateAsync());
        ExecuteInspectionCommand = new RelayCommand<object?>(_ => _ = ExecuteInspectionAsync());

        RefreshScopePresentation();
    }

    public AmapMapOptions MapOptions { get; }

    public ObservableCollection<SelectionItemViewModel> SchemeItems { get; }

    public ObservableCollection<SelectionItemViewModel> TaskTypeItems { get; }

    public ObservableCollection<OverviewMetric> OverviewMetrics { get; }

    public ObservableCollection<AlertItem> AlertItems { get; }

    public ObservableCollection<InspectionScopeMapPoint> MapPoints { get; }

    public ObservableCollection<SelectionItemViewModel> MissingCoordinateItems { get; }

    public CurrentTaskStatus CurrentTask { get; }

    public DeviceMediaReviewViewModel MediaReview => _mediaReview;

    public DeviceInspectionResult? SelectedInspectionResult
    {
        get => _selectedInspectionResult;
        private set => SetProperty(ref _selectedInspectionResult, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string MapCaption => $"地图巡检台 / {_currentSchemeName} / {_selectedTaskType?.Title ?? "常规巡检"}";

    public string SelectedPointName
    {
        get => _selectedPointName;
        private set => SetProperty(ref _selectedPointName, value);
    }

    public string SelectedPointDeviceCode
    {
        get => _selectedPointDeviceCode;
        private set => SetProperty(ref _selectedPointDeviceCode, value);
    }

    public string SelectedPointStatusText
    {
        get => _selectedPointStatusText;
        private set => SetProperty(ref _selectedPointStatusText, value);
    }

    public string SelectedPointDirectoryPath
    {
        get => _selectedPointDirectoryPath;
        private set => SetProperty(ref _selectedPointDirectoryPath, value);
    }

    public string SelectedCoordinateSourceText
    {
        get => _selectedCoordinateSourceText;
        private set => SetProperty(ref _selectedCoordinateSourceText, value);
    }

    public string SelectedCoordinateText
    {
        get => _selectedCoordinateText;
        private set => SetProperty(ref _selectedCoordinateText, value);
    }

    public string SelectedCoordinateUpdatedText
    {
        get => _selectedCoordinateUpdatedText;
        private set => SetProperty(ref _selectedCoordinateUpdatedText, value);
    }

    public string SelectedCoordinateRemarkText
    {
        get => _selectedCoordinateRemarkText;
        private set => SetProperty(ref _selectedCoordinateRemarkText, value);
    }

    public string EditorLongitude
    {
        get => _editorLongitude;
        set => SetProperty(ref _editorLongitude, value);
    }

    public string EditorLatitude
    {
        get => _editorLatitude;
        set => SetProperty(ref _editorLatitude, value);
    }

    public string EditorRemark
    {
        get => _editorRemark;
        set => SetProperty(ref _editorRemark, value);
    }

    public string EditorStatusText
    {
        get => _editorStatusText;
        private set => SetProperty(ref _editorStatusText, value);
    }

    public string EditorErrorText
    {
        get => _editorErrorText;
        private set => SetProperty(ref _editorErrorText, value);
    }

    public string MapErrorText
    {
        get => _mapErrorText;
        private set => SetProperty(ref _mapErrorText, value);
    }

    public string InspectionStatusText
    {
        get => _inspectionStatusText;
        private set => SetProperty(ref _inspectionStatusText, value);
    }

    public string InspectionAlertText
    {
        get => _inspectionAlertText;
        private set => SetProperty(ref _inspectionAlertText, value);
    }

    public bool IsPickMode
    {
        get => _isPickMode;
        private set => SetProperty(ref _isPickMode, value);
    }

    public bool IsInspectingSelectedPoint
    {
        get => _isInspectingSelectedPoint;
        private set
        {
            if (SetProperty(ref _isInspectingSelectedPoint, value))
            {
                RaisePropertyChanged(nameof(CanInspectSelectedPoint));
                RaisePropertyChanged(nameof(InspectButtonText));
            }
        }
    }

    public bool HasSelectedPoint => _scopeResult?.MapPoints.Any(point =>
        string.Equals(point.DeviceCode, SelectedPointDeviceCode, StringComparison.OrdinalIgnoreCase)) == true;

    public bool HasMissingCoordinatePoints => MissingCoordinateItems.Count > 0;

    public bool CanInspectSelectedPoint => HasSelectedPoint && !IsInspectingSelectedPoint;

    public string InspectButtonText => IsInspectingSelectedPoint ? "巡检中..." : "执行基础巡检";

    public ICommand SelectSchemeCommand { get; }

    public ICommand SelectTaskTypeCommand { get; }

    public ICommand SelectMissingCoordinateCommand { get; }

    public ICommand TogglePickModeCommand { get; }

    public ICommand SaveManualCoordinateCommand { get; }

    public ICommand ClearManualCoordinateCommand { get; }

    public ICommand ExecuteInspectionCommand { get; }

    public void HandleMapPointSelected(string? deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return;
        }

        SelectPoint(deviceCode, syncSelection: true);
    }

    public void HandleMapCoordinatePicked(double longitude, double latitude)
    {
        if (!HasSelectedPoint)
        {
            EditorErrorText = "请先选择点位，再执行地图取点。";
            IsPickMode = false;
            return;
        }

        EditorLongitude = longitude.ToString("F6", CultureInfo.InvariantCulture);
        EditorLatitude = latitude.ToString("F6", CultureInfo.InvariantCulture);
        EditorErrorText = string.Empty;
        EditorStatusText = "已从地图取点，保存后将优先覆盖平台坐标。";
        IsPickMode = false;
    }

    public void ReportMapReady()
    {
        MapErrorText = string.Empty;
    }

    public void ReportMapError(string message)
    {
        MapErrorText = message;
    }

    private void OnScopeChanged(object? sender, EventArgs e)
    {
        if (_suppressScopeEvent)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(RefreshScopePresentation));
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            SelectPoint(_inspectionSelectionService.GetSelectedDeviceCode(), syncSelection: false);
        }));
    }

    private void RefreshScopePresentation()
    {
        var scopeResult = _inspectionScopeService.GetCurrentScope();
        var schemes = _inspectionScopeService.GetSchemes();

        _scopeResult = scopeResult;
        _currentSchemeName = scopeResult.CurrentScheme.Name;
        LastUpdatedText = $"范围联动 {scopeResult.GeneratedAt:yyyy-MM-dd HH:mm}";

        ReplaceCollection(
            SchemeItems,
            schemes.Select(scheme => new SelectionItemViewModel
            {
                Key = scheme.Id,
                Title = scheme.IsDefault ? $"{scheme.Name} / 默认" : scheme.Name,
                Subtitle = string.IsNullOrWhiteSpace(scheme.Description)
                    ? BuildRuleSummary(scheme)
                    : scheme.Description,
                IsSelected = string.Equals(scheme.Id, scopeResult.CurrentScheme.Id, StringComparison.OrdinalIgnoreCase)
            }));

        _selectedScheme = SchemeItems.FirstOrDefault(item => item.IsSelected) ?? SchemeItems.FirstOrDefault();

        ReplaceCollection(
            OverviewMetrics,
            [
                BuildMetric("覆盖点位", scopeResult.Summary.CoveredPointCount.ToString(), "个", scopeResult.CurrentScheme.Name, "TonePrimaryBrush"),
                BuildMetric("在线点位", scopeResult.Summary.OnlinePointCount.ToString(), "个", $"离线 {scopeResult.Summary.OfflinePointCount}", "ToneSuccessBrush"),
                BuildMetric("离线点位", scopeResult.Summary.OfflinePointCount.ToString(), "个", "与当前方案联动", "ToneWarningBrush"),
                BuildMetric("坐标完备", scopeResult.Summary.WithCoordinatePointCount.ToString(), "个", $"缺失 {scopeResult.Summary.WithoutCoordinatePointCount}", "ToneInfoBrush"),
                BuildMetric("重点关注", scopeResult.Summary.FocusPointCount.ToString(), "个", "地图与点位治理共用口径", "ToneFocusBrush")
            ]);

        ReplaceCollection(MapPoints, scopeResult.MapPoints);
        ReplaceCollection(
            MissingCoordinateItems,
            scopeResult.MapPoints
                .Where(point => !point.Longitude.HasValue || !point.Latitude.HasValue)
                .Select(point => new SelectionItemViewModel
                {
                    Key = point.DeviceCode,
                    Title = point.DeviceName,
                    Subtitle = point.IsFocused
                        ? point.IsOnline ? "重点关注 / 在线 / 待补录坐标" : "重点关注 / 离线 / 待补录坐标"
                        : point.IsOnline ? "在线 / 待补录坐标" : "离线 / 待补录坐标",
                    IsSelected = string.Equals(point.DeviceCode, SelectedPointDeviceCode, StringComparison.OrdinalIgnoreCase)
                }));

        RaisePropertyChanged(nameof(HasMissingCoordinatePoints));
        RaisePropertyChanged(nameof(MapCaption));
        RaisePropertyChanged(nameof(CanInspectSelectedPoint));

        if (!SelectPoint(_inspectionSelectionService.GetSelectedDeviceCode(), syncSelection: false) &&
            !SelectPoint(SelectedPointDeviceCode, syncSelection: false))
        {
            ClearSelectedPoint(syncSelection: false);
        }
    }

    private async Task SelectSchemeAsync(SelectionItemViewModel? item)
    {
        if (item?.Key is null || string.Equals(item.Key, _selectedScheme?.Key, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _suppressScopeEvent = true;
        try
        {
            await Task.Run(() => _inspectionScopeService.SetCurrentScheme(item.Key));
        }
        finally
        {
            _suppressScopeEvent = false;
        }

        RefreshScopePresentation();
    }

    private void TogglePickMode()
    {
        if (!HasSelectedPoint)
        {
            EditorErrorText = "请先选择点位，再开启取点模式。";
            return;
        }

        IsPickMode = !IsPickMode;
        EditorErrorText = string.Empty;
        EditorStatusText = IsPickMode
            ? "取点模式已开启，请在地图上单击目标位置。"
            : "取点模式已关闭，可继续手工录入或保存当前坐标。";
    }

    private async Task SaveManualCoordinateAsync()
    {
        if (!HasSelectedPoint)
        {
            EditorErrorText = "请先选择点位。";
            return;
        }

        if (!double.TryParse(EditorLongitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            !double.TryParse(EditorLatitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
        {
            EditorErrorText = "请输入有效的 GCJ-02 经度和纬度。";
            return;
        }

        try
        {
            _manualCoordinateService.Save(new ManualCoordinateRecord
            {
                DeviceCode = SelectedPointDeviceCode,
                Longitude = longitude,
                Latitude = latitude,
                Remark = EditorRemark.Trim(),
                UpdatedAt = DateTimeOffset.Now,
                CoordinateSystem = MapOptions.CoordinateSystem
            });

            _suppressScopeEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.RefreshScope());
            }
            finally
            {
                _suppressScopeEvent = false;
            }

            EditorErrorText = string.Empty;
            EditorStatusText = "人工坐标已保存，地图已按当前方案静默刷新。";
            RefreshScopePresentation();
        }
        catch (Exception ex)
        {
            EditorErrorText = ex.Message;
        }
    }

    private async Task ClearManualCoordinateAsync()
    {
        if (!HasSelectedPoint)
        {
            EditorErrorText = "请先选择点位。";
            return;
        }

        try
        {
            var removed = _manualCoordinateService.Clear(SelectedPointDeviceCode);
            _suppressScopeEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.RefreshScope());
            }
            finally
            {
                _suppressScopeEvent = false;
            }

            EditorErrorText = string.Empty;
            EditorStatusText = removed
                ? "人工坐标已清除，地图已回退到平台坐标口径。"
                : "当前点位没有人工坐标，已保持平台坐标口径。";
            RefreshScopePresentation();
        }
        catch (Exception ex)
        {
            EditorErrorText = ex.Message;
        }
    }

    private async Task ExecuteInspectionAsync()
    {
        if (_scopeResult is null || !HasSelectedPoint || IsInspectingSelectedPoint)
        {
            return;
        }

        var scopeDevice = _scopeResult.Devices.FirstOrDefault(item =>
            string.Equals(item.Device.DeviceCode, SelectedPointDeviceCode, StringComparison.OrdinalIgnoreCase));
        if (scopeDevice is null)
        {
            InspectionAlertText = "当前选中点位不在巡检范围方案内，无法执行基础巡检。";
            return;
        }

        IsInspectingSelectedPoint = true;
        InspectionAlertText = string.Empty;
        InspectionStatusText = $"正在对点位“{scopeDevice.Device.DeviceName}”执行基础巡检...";

        try
        {
            var result = await Task.Run(() => _deviceInspectionService.Inspect(scopeDevice));
            SelectedInspectionResult = result;
            SyncMediaReviewContext();
            InspectionStatusText = $"基础巡检完成：{result.PlaybackHealthSummary} / {result.RecheckText}";
            InspectionAlertText = result.IsAbnormal
                ? result.HasFailureReason ? result.FailureReasonText : result.SuggestionText
                : string.Empty;

            _suppressScopeEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.RefreshScope());
            }
            finally
            {
                _suppressScopeEvent = false;
            }

            RefreshScopePresentation();
        }
        catch (Exception ex)
        {
            InspectionAlertText = ex.Message;
            InspectionStatusText = "基础巡检执行失败。";
        }
        finally
        {
            IsInspectingSelectedPoint = false;
        }
    }

    private bool SelectPoint(string? deviceCode, bool syncSelection)
    {
        if (_scopeResult is null || string.IsNullOrWhiteSpace(deviceCode))
        {
            return false;
        }

        var mapPoint = _scopeResult.MapPoints.FirstOrDefault(point => string.Equals(point.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase));
        if (mapPoint is null)
        {
            return false;
        }

        var scopeDevice = _scopeResult.Devices.FirstOrDefault(item => string.Equals(item.Device.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase));
        var manualCoordinate = _manualCoordinateService.Get(deviceCode);

        SelectedPointName = mapPoint.DeviceName;
        SelectedPointDeviceCode = mapPoint.DeviceCode;
        SelectedPointStatusText = BuildStatusText(mapPoint);
        SelectedPointDirectoryPath = scopeDevice?.Device.DirectoryPath ?? "--";
        SelectedCoordinateSourceText = BuildCoordinateSourceText(mapPoint.CoordinateSource);
        SelectedCoordinateText = BuildCoordinateText(mapPoint.Longitude, mapPoint.Latitude);
        SelectedCoordinateUpdatedText = manualCoordinate?.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
            ?? scopeDevice?.Device.LastSyncedAt?.ToString("yyyy-MM-dd HH:mm")
            ?? "--";
        SelectedCoordinateRemarkText = string.IsNullOrWhiteSpace(manualCoordinate?.Remark)
            ? mapPoint.CoordinateSource == InspectionScopeCoordinateSource.Manual
                ? "已启用人工坐标，暂无备注。"
                : "当前未配置人工坐标备注。"
            : manualCoordinate!.Remark;
        SelectedInspectionResult = scopeDevice?.LatestInspection;
        SyncMediaReviewContext();

        EditorLongitude = manualCoordinate?.Longitude.ToString("F6", CultureInfo.InvariantCulture)
            ?? mapPoint.Longitude?.ToString("F6", CultureInfo.InvariantCulture)
            ?? string.Empty;
        EditorLatitude = manualCoordinate?.Latitude.ToString("F6", CultureInfo.InvariantCulture)
            ?? mapPoint.Latitude?.ToString("F6", CultureInfo.InvariantCulture)
            ?? string.Empty;
        EditorRemark = manualCoordinate?.Remark ?? string.Empty;
        EditorErrorText = string.Empty;
        EditorStatusText = mapPoint.CoordinateSource == InspectionScopeCoordinateSource.Manual
            ? "当前地图优先使用人工坐标。"
            : mapPoint.Longitude.HasValue && mapPoint.Latitude.HasValue
                ? "当前地图使用平台坐标，可按 GCJ-02 手工修正。"
                : "当前点位暂无坐标，可手工录入或开启取点模式。";
        InspectionStatusText = SelectedInspectionResult is null
            ? "当前点位尚未执行基础巡检，可直接发起单点巡检。"
            : $"最近基础巡检：{SelectedInspectionResult.PlaybackHealthSummary} / {SelectedInspectionResult.RecheckText}";
        InspectionAlertText = SelectedInspectionResult?.IsAbnormal == true
            ? SelectedInspectionResult.HasFailureReason ? SelectedInspectionResult.FailureReasonText : SelectedInspectionResult.SuggestionText
            : string.Empty;
        IsPickMode = false;

        UpdateMissingCoordinateSelection();
        RaisePropertyChanged(nameof(HasSelectedPoint));
        RaisePropertyChanged(nameof(CanInspectSelectedPoint));

        if (syncSelection)
        {
            PublishSelectedPoint();
        }

        return true;
    }

    private void ClearSelectedPoint(bool syncSelection)
    {
        SelectedPointName = "未选中点位";
        SelectedPointDeviceCode = "--";
        SelectedPointStatusText = "请点击地图点位或待补录列表。";
        SelectedPointDirectoryPath = "--";
        SelectedCoordinateSourceText = "待补录";
        SelectedCoordinateText = "--";
        SelectedCoordinateUpdatedText = "--";
        SelectedCoordinateRemarkText = "暂无人工备注";
        SelectedInspectionResult = null;
        SyncMediaReviewContext();
        EditorLongitude = string.Empty;
        EditorLatitude = string.Empty;
        EditorRemark = string.Empty;
        EditorErrorText = string.Empty;
        EditorStatusText = "支持手工录入 GCJ-02 坐标，也可开启取点模式后单击地图。";
        InspectionStatusText = "请选择点位后执行基础巡检。";
        InspectionAlertText = string.Empty;
        IsPickMode = false;

        UpdateMissingCoordinateSelection();
        RaisePropertyChanged(nameof(HasSelectedPoint));
        RaisePropertyChanged(nameof(CanInspectSelectedPoint));

        if (syncSelection)
        {
            PublishSelectedPoint();
        }
    }

    private void SyncMediaReviewContext()
    {
        if (_scopeResult is null || !HasSelectedPoint)
        {
            _mediaReview.Clear();
            return;
        }

        var scopeDevice = _scopeResult.Devices.FirstOrDefault(item =>
            string.Equals(item.Device.DeviceCode, SelectedPointDeviceCode, StringComparison.OrdinalIgnoreCase));

        if (scopeDevice is null)
        {
            _mediaReview.Clear();
            return;
        }

        _mediaReview.BindTarget(
            scopeDevice.Device.DeviceCode,
            scopeDevice.Device.DeviceName,
            scopeDevice.Device.NetTypeCode,
            SelectedInspectionResult);
    }

    private void PublishSelectedPoint()
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        _suppressSelectionSync = true;
        try
        {
            _inspectionSelectionService.SetSelectedDevice(SelectedPointDeviceCode == "--" ? null : SelectedPointDeviceCode);
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }

    private void UpdateMissingCoordinateSelection()
    {
        foreach (var item in MissingCoordinateItems)
        {
            item.IsSelected = string.Equals(item.Key, SelectedPointDeviceCode, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string BuildStatusText(InspectionScopeMapPoint point)
    {
        var segments = new List<string>();
        if (point.IsFocused)
        {
            segments.Add("重点关注");
        }

        segments.Add(point.IsOnline ? "在线" : "离线");
        segments.Add(point.PlaybackHealthGrade.HasValue ? $"等级 {point.PlaybackHealthGrade}" : "待检");

        if (point.NeedRecheck)
        {
            segments.Add("需复检");
        }

        return string.Join(" / ", segments);
    }

    private static string BuildCoordinateSourceText(InspectionScopeCoordinateSource source)
    {
        return source switch
        {
            InspectionScopeCoordinateSource.Manual => "人工坐标优先",
            InspectionScopeCoordinateSource.Platform => "平台坐标",
            _ => "待补录"
        };
    }

    private static string BuildCoordinateText(double? longitude, double? latitude)
    {
        return longitude.HasValue && latitude.HasValue
            ? $"{longitude.Value:F6} / {latitude.Value:F6}"
            : "--";
    }

    private static OverviewMetric BuildMetric(string label, string value, string unit, string deltaText, string accentResourceKey)
    {
        return new OverviewMetric
        {
            Label = label,
            Value = value,
            Unit = unit,
            DeltaText = deltaText,
            AccentResourceKey = accentResourceKey
        };
    }

    private static string BuildRuleSummary(InspectionScopeScheme scheme)
    {
        var directoryCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Include && rule.TargetType == InspectionScopeTargetType.Directory);
        var includeDeviceCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Include && rule.TargetType == InspectionScopeTargetType.Device);
        var excludeDeviceCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Exclude && rule.TargetType == InspectionScopeTargetType.Device);
        var focusCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Focus && rule.TargetType == InspectionScopeTargetType.Device);
        return $"目录 {directoryCount} / 纳入 {includeDeviceCount} / 排除 {excludeDeviceCount} / 重点 {focusCount}";
    }

    private static void SelectSingle(SelectionItemViewModel? selectedItem, IEnumerable<SelectionItemViewModel> items, Action<SelectionItemViewModel> onSelected)
    {
        if (selectedItem is null)
        {
            return;
        }

        foreach (var item in items)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }

        onSelected(selectedItem);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
