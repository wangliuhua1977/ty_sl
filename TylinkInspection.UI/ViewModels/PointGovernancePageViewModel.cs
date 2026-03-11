using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class PointGovernancePageViewModel : PageViewModelBase
{
    private const int PageSize = 20;
    private const string ManualCoordinateSourceText = "人工坐标";
    private const string PlatformCoordinateSourceText = "平台坐标";
    private const string MissingCoordinateSourceText = "待补录";

    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IInspectionSelectionService _inspectionSelectionService;
    private readonly IFaultClosureService _faultClosureService;
    private readonly DeviceMediaReviewViewModel _mediaReview;
    private readonly HashSet<string> _draftIncludedDeviceCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _draftExcludedDeviceCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _draftFocusedDeviceCodes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, FaultClosureLinkageSummary> EmptyClosureLookup =
        new Dictionary<string, FaultClosureLinkageSummary>(StringComparer.OrdinalIgnoreCase);

    private InspectionScopeResult? _scopeResult;
    private IReadOnlyList<DirectoryNode> _catalogDirectoryTree = Array.Empty<DirectoryNode>();
    private IReadOnlyList<DeviceDirectoryItem> _catalogDevices = Array.Empty<DeviceDirectoryItem>();
    private IReadOnlyList<DeviceDirectoryItem> _filteredDevices = Array.Empty<DeviceDirectoryItem>();
    private IReadOnlyDictionary<string, FaultClosureLinkageSummary> _closureLookup = EmptyClosureLookup;
    private InspectionDirectoryNodeViewModel? _selectedDirectory;
    private ScopeDeviceItemViewModel? _selectedDevice;
    private DevicePointProfile? _selectedDeviceProfile;
    private DeviceInspectionResult? _selectedInspectionResult;
    private InspectionScopeSchemeOptionViewModel? _selectedScheme;
    private SelectionItemViewModel? _selectedClosureFilter;
    private string _treeStatusText = "正在准备巡检范围目录视图。";
    private string _treeErrorText = string.Empty;
    private string _listStatusText = "正在准备点位列表。";
    private string _listErrorText = string.Empty;
    private string _detailStatusText = "请选择一个点位查看基础信息、路径回溯和后续巡检入口。";
    private string _detailErrorText = string.Empty;
    private string _inspectionStatusText = "请选择点位后执行基础巡检。";
    private string _inspectionAlertText = string.Empty;
    private string _editorSchemeName = string.Empty;
    private string _editorSchemeDescription = string.Empty;
    private string _editorStatusText = "可通过目录树、当前设备列表和重点关注标记组装新的巡检范围方案。";
    private string _editorErrorText = string.Empty;
    private string _editingSchemeId = string.Empty;
    private bool _isTreeLoading;
    private bool _isListLoading;
    private bool _isDetailLoading;
    private bool _isInspectingSelectedDevice;
    private bool _isCatalogPoolMode;
    private bool _isEditorOpen;
    private bool _editorIncludeCatalog;
    private bool _editorIsDefault;
    private bool _isSelectionReserved;
    private bool _isUpdatingSchemeSelection;
    private bool _suppressServiceEvent;
    private bool _suppressSelectionSync;
    private int _pageIndex;

    public PointGovernancePageViewModel(
        IDeviceCatalogService deviceCatalogService,
        IDeviceInspectionService deviceInspectionService,
        IInspectionScopeService inspectionScopeService,
        IInspectionSelectionService inspectionSelectionService,
        IFaultClosureService faultClosureService,
        IPlaybackReviewService playbackReviewService,
        IScreenshotSamplingService screenshotSamplingService,
        ICloudPlaybackService cloudPlaybackService)
        : base(
            "点位治理中心",
            "围绕巡检范围方案组织目录树、点位列表、路径回溯和地图数据过滤结果，先把“范围方案 → 点位治理 → 地图数据源”链路打通。")
    {
        _deviceCatalogService = deviceCatalogService;
        _deviceInspectionService = deviceInspectionService;
        _inspectionScopeService = inspectionScopeService;
        _inspectionSelectionService = inspectionSelectionService;
        _faultClosureService = faultClosureService;
        _mediaReview = new DeviceMediaReviewViewModel(playbackReviewService, screenshotSamplingService, cloudPlaybackService);
        _inspectionScopeService.ScopeChanged += OnScopeChanged;
        _inspectionSelectionService.SelectionChanged += OnSelectionChanged;
        _faultClosureService.OverviewChanged += OnFaultClosureChanged;

        SummaryCards = new ObservableCollection<OverviewMetric>();
        DirectoryNodes = new ObservableCollection<InspectionDirectoryNodeViewModel>();
        DeviceItems = new ObservableCollection<ScopeDeviceItemViewModel>();
        SchemeOptions = new ObservableCollection<InspectionScopeSchemeOptionViewModel>();
        ClosureFilterItems = new ObservableCollection<SelectionItemViewModel>(BuildClosureFilterItems());
        EditorDirectoryNodes = new ObservableCollection<InspectionDirectoryNodeViewModel>();
        _selectedClosureFilter = ClosureFilterItems.FirstOrDefault();

        RefreshDirectoryCommand = new RelayCommand<object?>(_ => _ = ForceRefreshScopeAsync());
        ShowDirectoryDevicesCommand = new RelayCommand<object?>(_ => ShowCurrentSchemeDevices());
        ShowAllDevicesCommand = new RelayCommand<object?>(_ => ShowCatalogPoolDevices());
        RefreshListCommand = new RelayCommand<object?>(_ => RefreshCurrentList());
        NextPageCommand = new RelayCommand<object?>(_ => MoveToNextPage());
        PreviousPageCommand = new RelayCommand<object?>(_ => MoveToPreviousPage());
        SelectClosureFilterCommand = new RelayCommand<SelectionItemViewModel>(item => SelectClosureFilter(item));
        RefreshDetailCommand = new RelayCommand<object?>(_ => _ = LoadSelectedDeviceProfileAsync(forceRefresh: true));
        ExecuteInspectionCommand = new RelayCommand<object?>(_ => _ = ExecuteInspectionAsync());
        ReserveInspectionEntryCommand = new RelayCommand<object?>(_ => ReserveInspectionEntry());
        NewSchemeCommand = new RelayCommand<object?>(_ => OpenNewSchemeEditor());
        EditSchemeCommand = new RelayCommand<object?>(_ => OpenCurrentSchemeEditor());
        DeleteSchemeCommand = new RelayCommand<object?>(_ => _ = DeleteCurrentSchemeAsync());
        SaveSchemeCommand = new RelayCommand<object?>(_ => _ = SaveSchemeAsync());
        CancelSchemeEditCommand = new RelayCommand<object?>(_ => CloseEditor());

        RebuildSummary();
        _ = InitializeAsync();
    }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<InspectionDirectoryNodeViewModel> DirectoryNodes { get; }

    public ObservableCollection<ScopeDeviceItemViewModel> DeviceItems { get; }

    public ObservableCollection<InspectionScopeSchemeOptionViewModel> SchemeOptions { get; }

    public ObservableCollection<SelectionItemViewModel> ClosureFilterItems { get; }

    public ObservableCollection<InspectionDirectoryNodeViewModel> EditorDirectoryNodes { get; }

    public DeviceMediaReviewViewModel MediaReview => _mediaReview;

    public InspectionDirectoryNodeViewModel? SelectedDirectory
    {
        get => _selectedDirectory;
        set
        {
            if (SetProperty(ref _selectedDirectory, value))
            {
                RaisePropertyChanged(nameof(SelectedDirectoryPathText));
                _pageIndex = 0;
                RebuildDevicePage(preferredDeviceCode: SelectedDevice?.DeviceCode);
            }
        }
    }

    public ScopeDeviceItemViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                if (value is not null)
                {
                    _mediaReview.Clear();
                }

                _ = LoadSelectedDeviceProfileAsync(forceRefresh: false);
                if (value is null)
                {
                    _mediaReview.Clear();
                }

                RaisePropertyChanged(nameof(HasSelectedDevice));
                RaisePropertyChanged(nameof(CanInspectSelectedDevice));
                RaiseClosureDetailChanged();
                PublishSelectedDevice();
            }
        }
    }

    public DevicePointProfile? SelectedDeviceProfile
    {
        get => _selectedDeviceProfile;
        private set
        {
            if (SetProperty(ref _selectedDeviceProfile, value))
            {
                RaisePropertyChanged(nameof(HasSelectedDevice));
                RaisePropertyChanged(nameof(InspectionEntryText));
            }
        }
    }

    public DeviceInspectionResult? SelectedInspectionResult
    {
        get => _selectedInspectionResult;
        private set
        {
            if (SetProperty(ref _selectedInspectionResult, value))
            {
                RaisePropertyChanged(nameof(InspectionEntryText));
            }
        }
    }

    public InspectionScopeSchemeOptionViewModel? SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (!SetProperty(ref _selectedScheme, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanDeleteCurrentScheme));

            if (!_isUpdatingSchemeSelection && value is not null)
            {
                _ = SwitchSchemeAsync(value.Id);
            }
        }
    }

    public SelectionItemViewModel? SelectedClosureFilter
    {
        get => _selectedClosureFilter;
        private set => SetProperty(ref _selectedClosureFilter, value);
    }

    public string TreeStatusText
    {
        get => _treeStatusText;
        private set => SetProperty(ref _treeStatusText, value);
    }

    public string TreeErrorText
    {
        get => _treeErrorText;
        private set => SetProperty(ref _treeErrorText, value);
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

    public string DetailStatusText
    {
        get => _detailStatusText;
        private set => SetProperty(ref _detailStatusText, value);
    }

    public string DetailErrorText
    {
        get => _detailErrorText;
        private set => SetProperty(ref _detailErrorText, value);
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

    public string EditorSchemeName
    {
        get => _editorSchemeName;
        set => SetProperty(ref _editorSchemeName, value);
    }

    public string EditorSchemeDescription
    {
        get => _editorSchemeDescription;
        set => SetProperty(ref _editorSchemeDescription, value);
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

    public bool EditorIncludeCatalog
    {
        get => _editorIncludeCatalog;
        set => SetProperty(ref _editorIncludeCatalog, value);
    }

    public bool EditorIsDefault
    {
        get => _editorIsDefault;
        set => SetProperty(ref _editorIsDefault, value);
    }

    public bool IsTreeLoading
    {
        get => _isTreeLoading;
        private set => SetProperty(ref _isTreeLoading, value);
    }

    public bool IsListLoading
    {
        get => _isListLoading;
        private set => SetProperty(ref _isListLoading, value);
    }

    public bool IsDetailLoading
    {
        get => _isDetailLoading;
        private set => SetProperty(ref _isDetailLoading, value);
    }

    public bool IsCatalogPoolMode
    {
        get => _isCatalogPoolMode;
        private set
        {
            if (SetProperty(ref _isCatalogPoolMode, value))
            {
                RaisePropertyChanged(nameof(CurrentModeText));
                RaisePropertyChanged(nameof(SelectedDirectoryPathText));
            }
        }
    }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        private set => SetProperty(ref _isEditorOpen, value);
    }

    public bool IsInspectingSelectedDevice
    {
        get => _isInspectingSelectedDevice;
        private set
        {
            if (SetProperty(ref _isInspectingSelectedDevice, value))
            {
                RaisePropertyChanged(nameof(CanInspectSelectedDevice));
                RaisePropertyChanged(nameof(InspectButtonText));
            }
        }
    }

    public bool HasNextPage => (_pageIndex + 1) * PageSize < _filteredDevices.Count;

    public bool HasPreviousPage => _pageIndex > 0;

    public bool HasSelectedDevice => SelectedDeviceProfile is not null;

    public bool CanInspectSelectedDevice => SelectedDevice is not null && !IsInspectingSelectedDevice;

    public bool CanDeleteCurrentScheme => SelectedScheme is { IsDefault: false };

    public string CurrentScopeSchemeText => _scopeResult?.CurrentScheme.Name ?? "未启用方案";

    public string CurrentScopeSchemeDescription => _scopeResult?.CurrentScheme.Description ?? "尚未加载巡检范围方案。";

    public string CurrentSchemeRuleSummary => BuildRuleSummary(_scopeResult?.CurrentScheme);

    public string CurrentModeText => IsCatalogPoolMode
        ? $"缓存点位池 / {CurrentScopeSchemeText}"
        : $"当前巡检范围 / {CurrentScopeSchemeText}";

    public string SelectedDirectoryPathText => SelectedDirectory?.FullPath
        ?? (IsCatalogPoolMode ? "当前显示缓存点位池全部目录" : "当前显示方案覆盖的全部目录");

    public string PageSummaryText => $"第 {_pageIndex + 1} 页，当前页 {DeviceItems.Count} 条 / 筛选结果 {_filteredDevices.Count} 条";

    public string InspectionEntryText => _isSelectionReserved
        ? SelectedInspectionResult is null
            ? "基础巡检入口已绑定到当前点位，可直接发起单点巡检。"
            : $"最近基础巡检已落库：{SelectedInspectionResult.PlaybackHealthSummary}。"
        : "基础巡检入口已就绪，可直接发起单点巡检。";

    public string InspectButtonText => IsInspectingSelectedDevice ? "巡检中..." : "执行基础巡检";

    public string EditorTitleText => string.IsNullOrWhiteSpace(_editingSchemeId)
        ? "新建巡检范围方案"
        : "编辑巡检范围方案";

    public string CurrentClosureFilterText => SelectedClosureFilter?.Title ?? "全部闭环";

    public string SelectedClosureStatusText => SelectedDevice?.ClosureStatusText ?? "未进入闭环";

    public string SelectedClosureReviewConclusionText => SelectedDevice?.ClosureReviewConclusionText ?? "--";

    public string SelectedClosureLatestRecheckText => SelectedDevice?.ClosureLatestRecheckText ?? "未复检";

    public string SelectedClosurePendingDispatchText => SelectedDevice?.ClosurePendingDispatchText ?? "否";

    public string SelectedClosurePendingRecheckText => SelectedDevice?.ClosurePendingRecheckText ?? "否";

    public string SelectedClosurePendingClearText => SelectedDevice?.ClosurePendingClearText ?? "否";

    public string SelectedClosurePendingFlagsText => SelectedDevice?.ClosurePendingFlagsText ?? "未进入闭环";

    public string SelectedClosureAccentResourceKey => SelectedDevice?.ClosureAccentResourceKey ?? "ToneInfoBrush";

    public ICommand RefreshDirectoryCommand { get; }

    public ICommand ShowDirectoryDevicesCommand { get; }

    public ICommand ShowAllDevicesCommand { get; }

    public ICommand RefreshListCommand { get; }

    public ICommand NextPageCommand { get; }

    public ICommand PreviousPageCommand { get; }

    public ICommand SelectClosureFilterCommand { get; }

    public ICommand RefreshDetailCommand { get; }

    public ICommand ExecuteInspectionCommand { get; }

    public ICommand ReserveInspectionEntryCommand { get; }

    public ICommand NewSchemeCommand { get; }

    public ICommand EditSchemeCommand { get; }

    public ICommand DeleteSchemeCommand { get; }

    public ICommand SaveSchemeCommand { get; }

    public ICommand CancelSchemeEditCommand { get; }

    private async Task InitializeAsync()
    {
        await ReloadScopeDataAsync(preserveSelection: false);
    }

    private void OnScopeChanged(object? sender, EventArgs e)
    {
        if (_suppressServiceEvent)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = ReloadScopeDataAsync(preserveSelection: true)));
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            TryApplySharedSelection(_inspectionSelectionService.GetSelectedDeviceCode());
        }));
    }

    private void OnFaultClosureChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(RefreshFaultClosurePresentation));
    }

    private async Task ReloadScopeDataAsync(bool preserveSelection)
    {
        var preferredDirectoryId = preserveSelection ? SelectedDirectory?.Id : null;
        var preferredDeviceCode = preserveSelection ? SelectedDevice?.DeviceCode : null;

        IsTreeLoading = true;
        IsListLoading = true;
        TreeErrorText = string.Empty;
        ListErrorText = string.Empty;
        TreeStatusText = "正在按当前巡检范围方案同步目录树...";
        ListStatusText = "正在同步点位列表和地图过滤数据源...";

        try
        {
            var scopeResult = await Task.Run(() => _inspectionScopeService.GetCurrentScope());
            var schemes = await Task.Run(() => _inspectionScopeService.GetSchemes());
            var fullTree = await Task.Run(() => _deviceCatalogService.GetCachedDirectoryTree());
            var fullDevices = await Task.Run(() => _deviceCatalogService.GetCachedDevices());
            var closureOverview = await Task.Run(() => _faultClosureService.GetOverview(new FaultClosureQuery()));

            ApplyScopeState(scopeResult, schemes, fullTree, fullDevices, closureOverview, preferredDirectoryId, preferredDeviceCode);
        }
        catch (Exception ex)
        {
            TreeErrorText = BuildErrorText(ex);
            ListErrorText = BuildErrorText(ex);
            TreeStatusText = DirectoryNodes.Count == 0 ? "巡检范围目录同步失败。" : TreeStatusText;
            ListStatusText = DeviceItems.Count == 0 ? "点位列表同步失败。" : ListStatusText;
        }
        finally
        {
            IsTreeLoading = false;
            IsListLoading = false;
            RebuildSummary();
        }
    }

    private void ApplyScopeState(
        InspectionScopeResult scopeResult,
        IReadOnlyList<InspectionScopeScheme> schemes,
        IReadOnlyList<DirectoryNode> fullTree,
        IReadOnlyList<DeviceDirectoryItem> fullDevices,
        FaultClosureOverview closureOverview,
        string? preferredDirectoryId,
        string? preferredDeviceCode)
    {
        _scopeResult = scopeResult;
        _closureLookup = FaultClosureLinkageSummary.BuildLookup(closureOverview.Records);
        _catalogDirectoryTree = fullTree;
        _catalogDevices = fullDevices
            .Where(item => !string.IsNullOrWhiteSpace(item.DeviceCode))
            .GroupBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        UpdateSchemeOptions(schemes, scopeResult.CurrentScheme.Id);
        RebuildDirectoryTree(preferredDirectoryId);
        RebuildDevicePage(preferredDeviceCode);
        TryApplySharedSelection(_inspectionSelectionService.GetSelectedDeviceCode(), preferredDeviceCode);

        TreeStatusText = DirectoryNodes.Count == 0
            ? "当前巡检范围没有可显示目录，可切换到缓存点位池继续治理。"
            : $"已按方案“{scopeResult.CurrentScheme.Name}”同步 {CountNodes(DirectoryNodes)} 个目录节点。";
        ListStatusText = $"已按“{CurrentModeText}”筛出 {_filteredDevices.Count} 个点位。";

        RaisePropertyChanged(nameof(CurrentScopeSchemeText));
        RaisePropertyChanged(nameof(CurrentScopeSchemeDescription));
        RaisePropertyChanged(nameof(CurrentSchemeRuleSummary));
        RaisePropertyChanged(nameof(CurrentModeText));
        RaisePropertyChanged(nameof(SelectedDirectoryPathText));
        RaisePropertyChanged(nameof(PageSummaryText));
        RaisePropertyChanged(nameof(EditorTitleText));
    }

    private void RefreshFaultClosurePresentation()
    {
        var overview = _faultClosureService.GetOverview(new FaultClosureQuery());
        _closureLookup = FaultClosureLinkageSummary.BuildLookup(overview.Records);
        RebuildDevicePage(SelectedDevice?.DeviceCode);
        RebuildSummary();
        RaiseClosureDetailChanged();
    }

    private void UpdateSchemeOptions(IReadOnlyList<InspectionScopeScheme> schemes, string currentSchemeId)
    {
        var lookup = _scopeResult?.Summary;
        var items = schemes
            .Select(scheme => new InspectionScopeSchemeOptionViewModel
            {
                Id = scheme.Id,
                Name = scheme.Name,
                Description = scheme.Description,
                IsDefault = scheme.IsDefault,
                SummaryText = string.Equals(scheme.Id, currentSchemeId, StringComparison.OrdinalIgnoreCase) && lookup is not null
                    ? $"覆盖 {lookup.CoveredPointCount} 点位 / 在线 {lookup.OnlinePointCount} / 重点 {lookup.FocusPointCount}"
                    : BuildRuleSummary(scheme)
            })
            .ToList();

        ReplaceCollection(SchemeOptions, items);

        _isUpdatingSchemeSelection = true;
        SelectedScheme = SchemeOptions.FirstOrDefault(item => string.Equals(item.Id, currentSchemeId, StringComparison.OrdinalIgnoreCase))
            ?? SchemeOptions.FirstOrDefault();
        _isUpdatingSchemeSelection = false;
    }

    private void RebuildDirectoryTree(string? preferredDirectoryId)
    {
        var sourceTree = IsCatalogPoolMode
            ? _catalogDirectoryTree
            : _scopeResult?.VisibleDirectoryNodes ?? Array.Empty<DirectoryNode>();
        var nodes = BuildDirectoryNodes(sourceTree, selectedIds: null);
        ReplaceCollection(DirectoryNodes, nodes);

        SelectedDirectory = FindNodeById(DirectoryNodes, preferredDirectoryId)
            ?? DirectoryNodes.FirstOrDefault();
    }

    private void RebuildDevicePage(string? preferredDeviceCode)
    {
        var scopeLookup = (_scopeResult?.Devices ?? Array.Empty<InspectionScopeDevice>())
            .ToDictionary(item => item.Device.DeviceCode, StringComparer.OrdinalIgnoreCase);
        var sourceDevices = IsCatalogPoolMode
            ? _catalogDevices
            : (_scopeResult?.Devices ?? Array.Empty<InspectionScopeDevice>())
                .Select(item => item.Device)
                .ToList();

        if (SelectedDirectory is not null)
        {
            var subtreeIds = CollectDirectoryIds(SelectedDirectory);
            _filteredDevices = sourceDevices
                .Where(device => !string.IsNullOrWhiteSpace(device.DirectoryId) && subtreeIds.Contains(device.DirectoryId))
                .ToList();
        }
        else
        {
            _filteredDevices = sourceDevices.ToList();
        }

        _filteredDevices = ApplyClosureFilter(_filteredDevices).ToList();

        var maxPageIndex = _filteredDevices.Count == 0
            ? 0
            : Math.Max(0, (_filteredDevices.Count - 1) / PageSize);
        _pageIndex = Math.Min(_pageIndex, maxPageIndex);

        var pageItems = _filteredDevices
            .Skip(_pageIndex * PageSize)
            .Take(PageSize)
            .Select(device =>
            {
                var scopeDevice = scopeLookup.GetValueOrDefault(device.DeviceCode);
                return new ScopeDeviceItemViewModel(
                    device,
                    isInCurrentScope: scopeDevice is not null,
                    isFocused: scopeDevice?.IsFocused ?? false,
                    latestInspection: scopeDevice?.LatestInspection,
                    closureSummary: ResolveClosureSummary(device.DeviceCode),
                    hasCoordinate: scopeDevice?.HasCoordinate ?? HasCoordinate(device),
                    longitude: scopeDevice?.Longitude ?? TryParseCoordinate(device.Longitude),
                    latitude: scopeDevice?.Latitude ?? TryParseCoordinate(device.Latitude),
                    coordinateSourceText: scopeDevice is not null
                        ? ResolveCoordinateSourceText(scopeDevice.CoordinateSource)
                        : HasCoordinate(device)
                            ? PlatformCoordinateSourceText
                            : MissingCoordinateSourceText,
                    isDraftIncluded: _draftIncludedDeviceCodes.Contains(device.DeviceCode),
                    isDraftExcluded: _draftExcludedDeviceCodes.Contains(device.DeviceCode),
                    isDraftFocused: _draftFocusedDeviceCodes.Contains(device.DeviceCode),
                    onDraftChanged: HandleDraftDeviceChanged);
            })
            .ToList();

        ReplaceCollection(DeviceItems, pageItems);
        SelectedDevice = DeviceItems.FirstOrDefault(item => string.Equals(item.DeviceCode, preferredDeviceCode, StringComparison.OrdinalIgnoreCase))
            ?? DeviceItems.FirstOrDefault();

        RaisePropertyChanged(nameof(PageSummaryText));
        RaisePropertyChanged(nameof(HasNextPage));
        RaisePropertyChanged(nameof(HasPreviousPage));
        RaiseClosureDetailChanged();
    }

    private async Task ForceRefreshScopeAsync()
    {
        if (IsTreeLoading || IsListLoading)
        {
            return;
        }

        TreeStatusText = "正在刷新目录树和缓存点位池...";
        ListStatusText = "正在刷新当前方案和地图过滤数据源...";
        TreeErrorText = string.Empty;
        ListErrorText = string.Empty;

        _suppressServiceEvent = true;
        try
        {
            await Task.Run(() => _inspectionScopeService.RefreshScope(forceCatalogRefresh: true));
        }
        finally
        {
            _suppressServiceEvent = false;
        }

        await ReloadScopeDataAsync(preserveSelection: true);
    }

    private void ShowCurrentSchemeDevices()
    {
        if (!IsCatalogPoolMode)
        {
            return;
        }

        IsCatalogPoolMode = false;
        _pageIndex = 0;
        RebuildDirectoryTree(SelectedDirectory?.Id);
        RebuildDevicePage(SelectedDevice?.DeviceCode);
        RebuildSummary();
    }

    private void ShowCatalogPoolDevices()
    {
        if (IsCatalogPoolMode)
        {
            return;
        }

        IsCatalogPoolMode = true;
        _pageIndex = 0;
        RebuildDirectoryTree(SelectedDirectory?.Id);
        RebuildDevicePage(SelectedDevice?.DeviceCode);
        RebuildSummary();
    }

    private void RefreshCurrentList()
    {
        RebuildDirectoryTree(SelectedDirectory?.Id);
        RebuildDevicePage(SelectedDevice?.DeviceCode);
        RebuildSummary();
    }

    private void MoveToNextPage()
    {
        if (!HasNextPage)
        {
            return;
        }

        _pageIndex++;
        RebuildDevicePage(SelectedDevice?.DeviceCode);
    }

    private void MoveToPreviousPage()
    {
        if (!HasPreviousPage)
        {
            return;
        }

        _pageIndex--;
        RebuildDevicePage(SelectedDevice?.DeviceCode);
    }

    private void SelectClosureFilter(SelectionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var option in ClosureFilterItems)
        {
            option.IsSelected = ReferenceEquals(option, item);
        }

        SelectedClosureFilter = item;
        _pageIndex = 0;
        RebuildDevicePage(SelectedDevice?.DeviceCode);
        RebuildSummary();
        RaisePropertyChanged(nameof(CurrentClosureFilterText));
        RaisePropertyChanged(nameof(PageSummaryText));
    }

    private async Task SwitchSchemeAsync(string schemeId)
    {
        if (_scopeResult is not null &&
            string.Equals(_scopeResult.CurrentScheme.Id, schemeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TreeStatusText = "正在切换巡检范围方案...";
        ListStatusText = "正在按新方案重建点位列表、统计卡和地图数据源...";
        TreeErrorText = string.Empty;
        ListErrorText = string.Empty;

        _suppressServiceEvent = true;
        try
        {
            await Task.Run(() => _inspectionScopeService.SetCurrentScheme(schemeId));
        }
        catch (Exception ex)
        {
            ListErrorText = BuildErrorText(ex);
            TreeErrorText = ListErrorText;
        }
        finally
        {
            _suppressServiceEvent = false;
        }

        IsCatalogPoolMode = false;
        _pageIndex = 0;
        await ReloadScopeDataAsync(preserveSelection: true);
    }

    private void OpenNewSchemeEditor()
    {
        _editingSchemeId = string.Empty;
        EditorSchemeName = string.Empty;
        EditorSchemeDescription = string.Empty;
        EditorIncludeCatalog = false;
        EditorIsDefault = false;
        EditorErrorText = string.Empty;
        EditorStatusText = "新方案默认从空白规则开始，可先勾选目录，再从当前设备列表补充纳入、排除和重点关注。";
        _draftIncludedDeviceCodes.Clear();
        _draftExcludedDeviceCodes.Clear();
        _draftFocusedDeviceCodes.Clear();

        ReplaceCollection(EditorDirectoryNodes, BuildDirectoryNodes(_catalogDirectoryTree, selectedIds: null));
        IsEditorOpen = true;
        RaisePropertyChanged(nameof(EditorTitleText));
        RebuildDevicePage(SelectedDevice?.DeviceCode);
    }

    private void OpenCurrentSchemeEditor()
    {
        var scheme = _scopeResult?.CurrentScheme;
        if (scheme is null)
        {
            return;
        }

        _editingSchemeId = scheme.Id;
        EditorSchemeName = scheme.Name;
        EditorSchemeDescription = scheme.Description;
        EditorIncludeCatalog = scheme.Rules.Any(rule =>
            rule.Action == InspectionScopeRuleAction.Include &&
            rule.TargetType == InspectionScopeTargetType.Catalog);
        EditorIsDefault = scheme.IsDefault;
        EditorErrorText = string.Empty;
        EditorStatusText = $"正在编辑方案“{scheme.Name}”，保存后点位列表、统计卡和地图数据源会同步联动。";

        _draftIncludedDeviceCodes.Clear();
        _draftExcludedDeviceCodes.Clear();
        _draftFocusedDeviceCodes.Clear();

        var selectedDirectoryIds = new HashSet<string>(
            scheme.Rules
                .Where(rule => rule.Action == InspectionScopeRuleAction.Include && rule.TargetType == InspectionScopeTargetType.Directory)
                .Select(rule => rule.TargetId),
            StringComparer.OrdinalIgnoreCase);

        foreach (var rule in scheme.Rules)
        {
            if (rule.TargetType != InspectionScopeTargetType.Device)
            {
                continue;
            }

            switch (rule.Action)
            {
                case InspectionScopeRuleAction.Include:
                    _draftIncludedDeviceCodes.Add(rule.TargetId);
                    break;
                case InspectionScopeRuleAction.Exclude:
                    _draftExcludedDeviceCodes.Add(rule.TargetId);
                    break;
                case InspectionScopeRuleAction.Focus:
                    _draftFocusedDeviceCodes.Add(rule.TargetId);
                    break;
            }
        }

        ReplaceCollection(EditorDirectoryNodes, BuildDirectoryNodes(_catalogDirectoryTree, selectedDirectoryIds));
        IsEditorOpen = true;
        RaisePropertyChanged(nameof(EditorTitleText));
        RebuildDevicePage(SelectedDevice?.DeviceCode);
    }

    private async Task SaveSchemeAsync()
    {
        try
        {
            var scheme = BuildSchemeFromEditor();

            _suppressServiceEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.SaveScheme(scheme));
            }
            finally
            {
                _suppressServiceEvent = false;
            }

            IsCatalogPoolMode = false;
            CloseEditor();
            await ReloadScopeDataAsync(preserveSelection: false);
        }
        catch (Exception ex)
        {
            EditorErrorText = BuildErrorText(ex);
        }
    }

    private async Task DeleteCurrentSchemeAsync()
    {
        var scheme = _scopeResult?.CurrentScheme;
        if (scheme is null)
        {
            return;
        }

        TreeErrorText = string.Empty;
        ListErrorText = string.Empty;

        try
        {
            _suppressServiceEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.DeleteScheme(scheme.Id));
            }
            finally
            {
                _suppressServiceEvent = false;
            }

            IsCatalogPoolMode = false;
            await ReloadScopeDataAsync(preserveSelection: false);
        }
        catch (Exception ex)
        {
            var message = BuildErrorText(ex);
            TreeErrorText = message;
            ListErrorText = message;
        }
    }

    private InspectionScopeScheme BuildSchemeFromEditor()
    {
        var name = EditorSchemeName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("巡检范围方案名称不能为空。");
        }

        var deviceLookup = _catalogDevices.ToDictionary(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase);
        var rules = new List<InspectionScopeRule>();

        if (EditorIncludeCatalog)
        {
            rules.Add(new InspectionScopeRule
            {
                Action = InspectionScopeRuleAction.Include,
                TargetType = InspectionScopeTargetType.Catalog,
                TargetId = "ALL",
                TargetName = "全部缓存点位"
            });
        }

        foreach (var node in EnumerateCheckedDirectories(EditorDirectoryNodes))
        {
            rules.Add(new InspectionScopeRule
            {
                Action = InspectionScopeRuleAction.Include,
                TargetType = InspectionScopeTargetType.Directory,
                TargetId = node.Id,
                TargetName = node.Name,
                TargetPath = node.FullPath
            });
        }

        foreach (var deviceCode in _draftIncludedDeviceCodes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            rules.Add(new InspectionScopeRule
            {
                Action = InspectionScopeRuleAction.Include,
                TargetType = InspectionScopeTargetType.Device,
                TargetId = deviceCode,
                TargetName = deviceLookup.GetValueOrDefault(deviceCode)?.DeviceName
            });
        }

        foreach (var deviceCode in _draftExcludedDeviceCodes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            rules.Add(new InspectionScopeRule
            {
                Action = InspectionScopeRuleAction.Exclude,
                TargetType = InspectionScopeTargetType.Device,
                TargetId = deviceCode,
                TargetName = deviceLookup.GetValueOrDefault(deviceCode)?.DeviceName
            });
        }

        foreach (var deviceCode in _draftFocusedDeviceCodes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            rules.Add(new InspectionScopeRule
            {
                Action = InspectionScopeRuleAction.Focus,
                TargetType = InspectionScopeTargetType.Device,
                TargetId = deviceCode,
                TargetName = deviceLookup.GetValueOrDefault(deviceCode)?.DeviceName
            });
        }

        EditorStatusText = $"已组装规则：目录 {rules.Count(rule => rule.TargetType == InspectionScopeTargetType.Directory)} / 单点纳入 {_draftIncludedDeviceCodes.Count} / 单点排除 {_draftExcludedDeviceCodes.Count} / 重点 {_draftFocusedDeviceCodes.Count}";
        EditorErrorText = string.Empty;

        return new InspectionScopeScheme
        {
            Id = _editingSchemeId,
            Name = name,
            Description = EditorSchemeDescription.Trim(),
            IsDefault = EditorIsDefault,
            Rules = rules,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private void CloseEditor()
    {
        IsEditorOpen = false;
        _editingSchemeId = string.Empty;
        EditorErrorText = string.Empty;
        EditorStatusText = "可通过目录树、当前设备列表和重点关注标记组装新的巡检范围方案。";
        RaisePropertyChanged(nameof(EditorTitleText));
        RebuildDevicePage(SelectedDevice?.DeviceCode);
    }

    private void HandleDraftDeviceChanged(ScopeDeviceItemViewModel item)
    {
        UpdateDraftSet(_draftIncludedDeviceCodes, item.DeviceCode, item.IsDraftIncluded);
        UpdateDraftSet(_draftExcludedDeviceCodes, item.DeviceCode, item.IsDraftExcluded);
        UpdateDraftSet(_draftFocusedDeviceCodes, item.DeviceCode, item.IsDraftFocused);
    }

    private async Task LoadSelectedDeviceProfileAsync(bool forceRefresh)
    {
        if (SelectedDevice is null || IsDetailLoading)
        {
            if (SelectedDevice is null)
            {
                SelectedDeviceProfile = null;
                SelectedInspectionResult = null;
                SyncMediaReviewContext();
                DetailStatusText = "请选择一个点位查看基础信息、路径回溯和后续巡检入口。";
                DetailErrorText = string.Empty;
                InspectionStatusText = "请选择点位后执行基础巡检。";
                InspectionAlertText = string.Empty;
            }

            return;
        }

        IsDetailLoading = true;
        DetailStatusText = forceRefresh ? "正在刷新点位详情..." : "正在加载点位详情...";
        DetailErrorText = string.Empty;
        _isSelectionReserved = false;
        RaisePropertyChanged(nameof(InspectionEntryText));
        InspectionAlertText = string.Empty;

        try
        {
            SelectedDeviceProfile = await Task.Run(() => _deviceCatalogService.GetDeviceProfile(SelectedDevice.DeviceCode, BuildProfileSeed(SelectedDevice)));
            SelectedInspectionResult = ResolveLatestInspection(SelectedDevice.DeviceCode);
            SyncMediaReviewContext();
            InspectionStatusText = SelectedInspectionResult is null
                ? "当前点位尚未执行基础巡检，可直接发起单点巡检。"
                : $"最近基础巡检：{SelectedInspectionResult.PlaybackHealthSummary} / {SelectedInspectionResult.RecheckText}";
            InspectionAlertText = SelectedInspectionResult?.IsAbnormal == true
                ? SelectedInspectionResult.HasFailureReason ? SelectedInspectionResult.FailureReasonText : SelectedInspectionResult.SuggestionText
                : string.Empty;
            DetailStatusText = $"已获取点位“{SelectedDevice.DeviceName}”的基础信息、连接节点和路径回溯，可直接复用于后续体检编排。";
        }
        catch (Exception ex)
        {
            SelectedInspectionResult = ResolveLatestInspection(SelectedDevice.DeviceCode);
            SyncMediaReviewContext();
            DetailErrorText = BuildErrorText(ex);
            DetailStatusText = "点位详情加载失败。";
            InspectionStatusText = SelectedInspectionResult is null
                ? "点位详情加载失败，但仍可执行基础巡检。"
                : $"最近基础巡检：{SelectedInspectionResult.PlaybackHealthSummary}";
        }
        finally
        {
            IsDetailLoading = false;
        }
    }

    private async Task ExecuteInspectionAsync()
    {
        if (SelectedDevice is null || IsInspectingSelectedDevice)
        {
            return;
        }

        var selectedDeviceCode = SelectedDevice.DeviceCode;
        IsInspectingSelectedDevice = true;
        InspectionAlertText = string.Empty;
        InspectionStatusText = $"正在对点位“{SelectedDevice.DeviceName}”执行基础巡检...";

        try
        {
            var profile = SelectedDeviceProfile
                ?? await Task.Run(() => _deviceCatalogService.GetDeviceProfile(selectedDeviceCode, BuildProfileSeed(SelectedDevice)));
            SelectedDeviceProfile ??= profile;

            var result = await Task.Run(() => _deviceInspectionService.Inspect(profile));
            SelectedInspectionResult = result;
            SyncMediaReviewContext();
            _isSelectionReserved = true;
            RaisePropertyChanged(nameof(InspectionEntryText));

            InspectionStatusText = $"基础巡检完成：{result.PlaybackHealthSummary} / {result.RecheckText}";
            InspectionAlertText = result.IsAbnormal
                ? result.HasFailureReason ? result.FailureReasonText : result.SuggestionText
                : string.Empty;

            _suppressServiceEvent = true;
            try
            {
                await Task.Run(() => _inspectionScopeService.RefreshScope());
            }
            finally
            {
                _suppressServiceEvent = false;
            }

            await ReloadScopeDataAsync(preserveSelection: true);
        }
        catch (Exception ex)
        {
            InspectionAlertText = BuildErrorText(ex);
            InspectionStatusText = "基础巡检执行失败。";
        }
        finally
        {
            IsInspectingSelectedDevice = false;
        }
    }

    private void ReserveInspectionEntry()
    {
        if (SelectedDeviceProfile is null)
        {
            DetailStatusText = "请先选择点位，再预留体检入口。";
            return;
        }

        _isSelectionReserved = true;
        DetailStatusText = $"体检入口已为点位“{SelectedDeviceProfile.Device.DeviceName}”预留，后续只需挂接体检任务和结果看板。";
        InspectionStatusText = "基础巡检入口已绑定到当前点位，可直接执行单点巡检。";
        RaisePropertyChanged(nameof(InspectionEntryText));
    }

    private void RebuildSummary()
    {
        var summary = _scopeResult?.Summary;
        var currentPageFocusCount = DeviceItems.Count(item => item.IsFocused);

        ReplaceCollection(
            SummaryCards,
            [
                BuildMetric("方案覆盖", (summary?.CoveredPointCount ?? 0).ToString(), "点位", CurrentScopeSchemeText, "TonePrimaryBrush"),
                BuildMetric("在线点位", (summary?.OnlinePointCount ?? 0).ToString(), "点位", $"离线 {summary?.OfflinePointCount ?? 0}", "ToneSuccessBrush"),
                BuildMetric("坐标完备", (summary?.WithCoordinatePointCount ?? 0).ToString(), "点位", $"缺失 {summary?.WithoutCoordinatePointCount ?? 0}", "ToneInfoBrush"),
                BuildMetric("重点关注", (summary?.FocusPointCount ?? 0).ToString(), "点位", $"当前页 {currentPageFocusCount} 个", "ToneWarningBrush")
            ]);
    }

    private FaultClosureLinkageSummary ResolveClosureSummary(string? deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return FaultClosureLinkageSummary.Empty;
        }

        return _closureLookup.TryGetValue(deviceCode, out var summary)
            ? summary
            : FaultClosureLinkageSummary.Empty;
    }

    private IEnumerable<DeviceDirectoryItem> ApplyClosureFilter(IEnumerable<DeviceDirectoryItem> devices)
    {
        return devices.Where(device =>
        {
            var summary = ResolveClosureSummary(device.DeviceCode);
            return SelectedClosureFilter?.Key switch
            {
                "PendingDispatch" => summary.IsPendingDispatch,
                "PendingRecheck" => summary.IsPendingRecheck,
                "PendingClear" => summary.IsPendingClear,
                "FalsePositiveClosed" => summary.IsFalsePositiveClosed,
                _ => true
            };
        });
    }

    private void RaiseClosureDetailChanged()
    {
        RaisePropertyChanged(nameof(CurrentClosureFilterText));
        RaisePropertyChanged(nameof(SelectedClosureStatusText));
        RaisePropertyChanged(nameof(SelectedClosureReviewConclusionText));
        RaisePropertyChanged(nameof(SelectedClosureLatestRecheckText));
        RaisePropertyChanged(nameof(SelectedClosurePendingDispatchText));
        RaisePropertyChanged(nameof(SelectedClosurePendingRecheckText));
        RaisePropertyChanged(nameof(SelectedClosurePendingClearText));
        RaisePropertyChanged(nameof(SelectedClosurePendingFlagsText));
        RaisePropertyChanged(nameof(SelectedClosureAccentResourceKey));
    }

    private static IEnumerable<SelectionItemViewModel> BuildClosureFilterItems()
    {
        return
        [
            new SelectionItemViewModel { Key = string.Empty, Title = "全部闭环", IsSelected = true },
            new SelectionItemViewModel { Key = "PendingDispatch", Title = "仅看待派单" },
            new SelectionItemViewModel { Key = "PendingRecheck", Title = "仅看待复检" },
            new SelectionItemViewModel { Key = "PendingClear", Title = "仅看待销警" },
            new SelectionItemViewModel { Key = "FalsePositiveClosed", Title = "仅看误报关闭" }
        ];
    }

    private static bool HasCoordinate(DeviceDirectoryItem device)
    {
        return !string.IsNullOrWhiteSpace(device.Longitude) &&
               !string.IsNullOrWhiteSpace(device.Latitude);
    }

    private static DeviceDirectoryItem BuildProfileSeed(ScopeDeviceItemViewModel selectedDevice)
    {
        return new DeviceDirectoryItem
        {
            DeviceCode = selectedDevice.Device.DeviceCode,
            DeviceName = selectedDevice.Device.DeviceName,
            OnlineStatus = selectedDevice.Device.OnlineStatus,
            OnlineStatusText = selectedDevice.Device.OnlineStatusText,
            DirectoryId = selectedDevice.Device.DirectoryId,
            DirectoryName = selectedDevice.Device.DirectoryName,
            DirectoryPath = selectedDevice.Device.DirectoryPath,
            DeviceSource = selectedDevice.Device.DeviceSource,
            DeviceSourceText = selectedDevice.Device.DeviceSourceText,
            Longitude = selectedDevice.Longitude?.ToString(CultureInfo.InvariantCulture),
            Latitude = selectedDevice.Latitude?.ToString(CultureInfo.InvariantCulture),
            RegionCode = selectedDevice.Device.RegionCode,
            RegionGbId = selectedDevice.Device.RegionGbId,
            GbId = selectedDevice.Device.GbId,
            SourceGbId = selectedDevice.Device.SourceGbId,
            NodeId = selectedDevice.Device.NodeId,
            NetTypeCode = selectedDevice.Device.NetTypeCode,
            NetTypeText = selectedDevice.Device.NetTypeText,
            LastSyncedAt = selectedDevice.Device.LastSyncedAt
        };
    }

    private DeviceInspectionResult? ResolveLatestInspection(string? deviceCode)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            return null;
        }

        var scopeInspection = _scopeResult?.Devices
            .FirstOrDefault(item => string.Equals(item.Device.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase))
            ?.LatestInspection;
        return scopeInspection ?? _deviceInspectionService.GetLatestResult(deviceCode);
    }

    private void SyncMediaReviewContext()
    {
        if (SelectedDevice is null)
        {
            _mediaReview.Clear();
            return;
        }

        var deviceName = SelectedDeviceProfile?.Device.DeviceName ?? SelectedDevice.DeviceName;
        var netTypeCode = SelectedDeviceProfile?.Device.NetTypeCode ?? SelectedDevice.Device.NetTypeCode;
        _mediaReview.BindTarget(SelectedDevice.DeviceCode, deviceName, netTypeCode, SelectedInspectionResult);
    }

    private void PublishSelectedDevice()
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        _inspectionSelectionService.SetSelectedDevice(SelectedDevice?.DeviceCode);
    }

    private void TryApplySharedSelection(string? deviceCode, string? fallbackDeviceCode = null)
    {
        var targetCode = string.IsNullOrWhiteSpace(deviceCode)
            ? fallbackDeviceCode
            : deviceCode;
        if (string.IsNullOrWhiteSpace(targetCode))
        {
            return;
        }

        _suppressSelectionSync = true;
        try
        {
            SelectDeviceByCode(targetCode);
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }

    private void SelectDeviceByCode(string deviceCode)
    {
        var sourceDevices = IsCatalogPoolMode
            ? _catalogDevices
            : (_scopeResult?.Devices ?? Array.Empty<InspectionScopeDevice>())
                .Select(item => item.Device)
                .ToList();
        var targetDevice = sourceDevices.FirstOrDefault(item => string.Equals(item.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase));
        if (targetDevice is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(targetDevice.DirectoryId))
        {
            var targetDirectory = FindNodeById(DirectoryNodes, targetDevice.DirectoryId);
            if (targetDirectory is not null && !ReferenceEquals(SelectedDirectory, targetDirectory))
            {
                SelectedDirectory = targetDirectory;
            }
        }

        var targetIndex = _filteredDevices
            .Select((device, index) => new { device, index })
            .FirstOrDefault(item => string.Equals(item.device.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase))
            ?.index;
        if (targetIndex.HasValue)
        {
            var targetPageIndex = targetIndex.Value / PageSize;
            if (_pageIndex != targetPageIndex)
            {
                _pageIndex = targetPageIndex;
                RebuildDevicePage(deviceCode);
                return;
            }
        }

        var targetItem = DeviceItems.FirstOrDefault(item => string.Equals(item.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase));
        if (targetItem is not null)
        {
            SelectedDevice = targetItem;
        }
    }

    private static double? TryParseCoordinate(string? coordinateText)
    {
        return double.TryParse(coordinateText, out var value) ? value : null;
    }

    private static string ResolveCoordinateSourceText(InspectionScopeCoordinateSource source)
    {
        return source switch
        {
            InspectionScopeCoordinateSource.Manual => ManualCoordinateSourceText,
            InspectionScopeCoordinateSource.Platform => PlatformCoordinateSourceText,
            _ => MissingCoordinateSourceText
        };
    }

    private static void UpdateDraftSet(ISet<string> target, string deviceCode, bool enabled)
    {
        if (enabled)
        {
            target.Add(deviceCode);
        }
        else
        {
            target.Remove(deviceCode);
        }
    }

    private static IEnumerable<InspectionDirectoryNodeViewModel> EnumerateCheckedDirectories(
        IEnumerable<InspectionDirectoryNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsChecked)
            {
                yield return node;
            }

            foreach (var child in EnumerateCheckedDirectories(node.Children))
            {
                yield return child;
            }
        }
    }

    private static ObservableCollection<InspectionDirectoryNodeViewModel> BuildDirectoryNodes(
        IEnumerable<DirectoryNode> nodes,
        ISet<string>? selectedIds)
    {
        return new ObservableCollection<InspectionDirectoryNodeViewModel>(
            nodes.Select(node => new InspectionDirectoryNodeViewModel
            {
                Id = node.Id,
                ParentId = node.ParentId,
                Name = node.Name,
                FullPath = node.FullPath,
                Level = node.Level,
                HasChildren = node.HasChildren,
                HasDevice = node.HasDevice,
                IsChecked = selectedIds?.Contains(node.Id) == true,
                Children = BuildDirectoryNodes(node.Children, selectedIds)
            }));
    }

    private static HashSet<string> CollectDirectoryIds(InspectionDirectoryNodeViewModel node)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { node.Id };
        foreach (var child in node.Children)
        {
            results.UnionWith(CollectDirectoryIds(child));
        }

        return results;
    }

    private static InspectionDirectoryNodeViewModel? FindNodeById(
        IEnumerable<InspectionDirectoryNodeViewModel> nodes,
        string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var child = FindNodeById(node.Children, id);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static int CountNodes(IEnumerable<InspectionDirectoryNodeViewModel> nodes)
    {
        return nodes.Sum(node => 1 + CountNodes(node.Children));
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

    private static string BuildRuleSummary(InspectionScopeScheme? scheme)
    {
        if (scheme is null)
        {
            return "尚未加载巡检范围规则。";
        }

        var directoryCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Include && rule.TargetType == InspectionScopeTargetType.Directory);
        var includeDeviceCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Include && rule.TargetType == InspectionScopeTargetType.Device);
        var excludeDeviceCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Exclude && rule.TargetType == InspectionScopeTargetType.Device);
        var focusCount = scheme.Rules.Count(rule => rule.Action == InspectionScopeRuleAction.Focus && rule.TargetType == InspectionScopeTargetType.Device);
        var includeCatalog = scheme.Rules.Any(rule => rule.Action == InspectionScopeRuleAction.Include && rule.TargetType == InspectionScopeTargetType.Catalog);

        return $"全量缓存 {(includeCatalog ? "已纳入" : "未纳入")} / 目录 {directoryCount} / 单点纳入 {includeDeviceCount} / 单点排除 {excludeDeviceCount} / 重点 {focusCount}";
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static string BuildErrorText(Exception exception)
    {
        if (exception is PlatformServiceException platformException)
        {
            return string.IsNullOrWhiteSpace(platformException.ErrorCode)
                ? $"{platformException.Category}: {platformException.Message}"
                : $"{platformException.Category} [{platformException.ErrorCode}]: {platformException.Message}";
        }

        return exception.Message;
    }
}
