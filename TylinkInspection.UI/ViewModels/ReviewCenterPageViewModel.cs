using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed partial class ReviewCenterPageViewModel : PageViewModelBase
{
    private const string WallModeAll = "All";
    private const string WallModeAbnormal = "Abnormal";
    private const string WallModeComparison = "Comparison";

    private readonly IReviewCenterService _reviewCenterService;
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IInspectionSelectionService _inspectionSelectionService;
    private readonly DeviceMediaReviewViewModel _mediaReview;

    private ReviewCenterOverview? _overview;
    private InspectionScopeResult? _scopeResult;
    private IReadOnlyDictionary<string, InspectionScopeDevice> _scopeDeviceLookup =
        new Dictionary<string, InspectionScopeDevice>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ReviewEvidenceItem> _allEvidenceItems = Array.Empty<ReviewEvidenceItem>();
    private SelectionItemViewModel? _selectedScheme;
    private SelectionItemViewModel? _selectedWallMode;
    private SelectionItemViewModel? _selectedConclusion;
    private ReviewEvidenceItem? _selectedEvidence;
    private InspectionScopeDevice? _selectedScopeDevice;
    private string _statusText = "正在汇总当前方案内截图样本...";
    private string _warningText = string.Empty;
    private string _saveStatusText = "选择截图样本后可写回复核结论。";
    private string _saveErrorText = string.Empty;
    private string _reviewer = ResolveDefaultReviewer();
    private string _reviewRemark = string.Empty;
    private bool _requiresDispatch;
    private bool _requiresRecheck;
    private bool _prioritizeFocused = true;
    private bool _prioritizeAbnormal = true;
    private bool _pendingOnly;
    private bool _isRefreshing;
    private bool _isSavingManualReview;
    private bool _isUpdatingSchemeSelection;
    private bool _suppressScopeEvent;
    private bool _suppressSelectionSync;

    public ReviewCenterPageViewModel(
        ModulePageData pageData,
        IReviewCenterService reviewCenterService,
        IInspectionScopeService inspectionScopeService,
        IInspectionSelectionService inspectionSelectionService,
        IInspectionModuleNavigationService moduleNavigationService,
        IAiInspectionTaskService aiInspectionTaskService,
        IPlaybackReviewService playbackReviewService,
        IScreenshotSamplingService screenshotSamplingService,
        ICloudPlaybackService cloudPlaybackService)
        : base(
            pageData.PageTitle,
            "汇总当前方案内直播截图、回看截图与 AI 画面异常证据，支持人工快速复核、写回结论与快速播放联动。")
    {
        _reviewCenterService = reviewCenterService;
        _inspectionScopeService = inspectionScopeService;
        _inspectionSelectionService = inspectionSelectionService;
        _moduleNavigationService = moduleNavigationService;
        _aiInspectionTaskService = aiInspectionTaskService;
        _mediaReview = new DeviceMediaReviewViewModel(playbackReviewService, screenshotSamplingService, cloudPlaybackService);

        StatusBadgeText = pageData.StatusBadgeText;
        StatusBadgeAccentResourceKey = pageData.StatusBadgeAccentResourceKey;

        SummaryCards = new ObservableCollection<OverviewMetric>();
        SchemeItems = new ObservableCollection<SelectionItemViewModel>();
        WallModeItems = new ObservableCollection<SelectionItemViewModel>(
        [
            new SelectionItemViewModel
            {
                Key = WallModeAll,
                Title = "全点位截图墙",
                Subtitle = "按当前方案汇总直播截图、回看截图与 AI 证据图。",
                Badge = "ALL",
                IsSelected = true
            },
            new SelectionItemViewModel
            {
                Key = WallModeAbnormal,
                Title = "异常优先墙",
                Subtitle = "优先展示画面异常、AI 异常、连续异常与待人工复核样本。",
                Badge = "ALERT"
            },
            new SelectionItemViewModel
            {
                Key = WallModeComparison,
                Title = "同刻对照墙",
                Subtitle = "预留与后续同刻对照、同源多样本复核联动。",
                Badge = "HOLD"
            }
        ]);
        ConclusionItems = new ObservableCollection<SelectionItemViewModel>(
        [
            BuildConclusionItem(ManualReviewConclusions.Normal, "正常", "确认画面正常。"),
            BuildConclusionItem(ManualReviewConclusions.BlackScreen, "黑屏", "确认存在黑屏。"),
            BuildConclusionItem(ManualReviewConclusions.FrozenFrame, "冻帧", "确认画面卡死或静止。"),
            BuildConclusionItem(ManualReviewConclusions.Tilted, "偏斜", "确认镜头偏斜。"),
            BuildConclusionItem(ManualReviewConclusions.Obstruction, "遮挡", "确认镜头被遮挡。"),
            BuildConclusionItem(ManualReviewConclusions.Blur, "模糊", "确认画面清晰度异常。"),
            BuildConclusionItem(ManualReviewConclusions.LowLight, "低照度", "确认低照度导致不可辨识。"),
            BuildConclusionItem(ManualReviewConclusions.FalsePositive, "误报", "确认机器判定为误报。")
        ]);
        DisplayedEvidenceCards = new ObservableCollection<ReviewEvidenceCardViewModel>();

        _selectedWallMode = WallModeItems.FirstOrDefault(item => item.IsSelected) ?? WallModeItems.FirstOrDefault();

        _inspectionScopeService.ScopeChanged += OnScopeChanged;
        _inspectionSelectionService.SelectionChanged += OnSelectionChanged;
        _moduleNavigationService.NavigationRequested += OnNavigationRequested;

        RefreshCommand = new RelayCommand<object?>(_ => _ = RefreshOverviewAsync(forceRefreshAiAlerts: false, preserveSelection: true));
        RefreshAiAlertsCommand = new RelayCommand<object?>(_ => _ = RefreshOverviewAsync(forceRefreshAiAlerts: true, preserveSelection: true));
        SelectWallModeCommand = new RelayCommand<SelectionItemViewModel>(SelectWallMode);
        SelectEvidenceCommand = new RelayCommand<ReviewEvidenceCardViewModel>(card => SelectEvidence(card?.Evidence, publishSelection: true));
        SaveManualReviewCommand = new RelayCommand<object?>(_ => _ = SaveManualReviewAsync());
        ResetReviewDraftCommand = new RelayCommand<object?>(_ => ResetReviewDraft());

        _ = RefreshOverviewAsync(forceRefreshAiAlerts: false, preserveSelection: false);
    }

    public string StatusBadgeText { get; }

    public string StatusBadgeAccentResourceKey { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<SelectionItemViewModel> SchemeItems { get; }

    public ObservableCollection<SelectionItemViewModel> WallModeItems { get; }

    public ObservableCollection<SelectionItemViewModel> ConclusionItems { get; }

    public ObservableCollection<ReviewEvidenceCardViewModel> DisplayedEvidenceCards { get; }

    public DeviceMediaReviewViewModel MediaReview => _mediaReview;

    public SelectionItemViewModel? SelectedScheme
    {
        get => _selectedScheme;
        set
        {
            if (!SetProperty(ref _selectedScheme, value))
            {
                return;
            }

            if (!_isUpdatingSchemeSelection && value?.Key is { Length: > 0 } schemeId)
            {
                _ = SwitchSchemeAsync(schemeId);
            }
        }
    }

    public SelectionItemViewModel? SelectedConclusion
    {
        get => _selectedConclusion;
        set
        {
            if (SetProperty(ref _selectedConclusion, value))
            {
                RaisePropertyChanged(nameof(CanSaveManualReview));
            }
        }
    }

    public ReviewEvidenceItem? SelectedEvidence => _selectedEvidence;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string WarningText
    {
        get => _warningText;
        private set => SetProperty(ref _warningText, value);
    }

    public string SaveStatusText
    {
        get => _saveStatusText;
        private set => SetProperty(ref _saveStatusText, value);
    }

    public string SaveErrorText
    {
        get => _saveErrorText;
        private set => SetProperty(ref _saveErrorText, value);
    }

    public string Reviewer
    {
        get => _reviewer;
        set
        {
            if (SetProperty(ref _reviewer, value))
            {
                RaisePropertyChanged(nameof(CanSaveManualReview));
            }
        }
    }

    public string ReviewRemark
    {
        get => _reviewRemark;
        set => SetProperty(ref _reviewRemark, value);
    }

    public bool RequiresDispatch
    {
        get => _requiresDispatch;
        set => SetProperty(ref _requiresDispatch, value);
    }

    public bool RequiresRecheck
    {
        get => _requiresRecheck;
        set => SetProperty(ref _requiresRecheck, value);
    }

    public bool PrioritizeFocused
    {
        get => _prioritizeFocused;
        set
        {
            if (SetProperty(ref _prioritizeFocused, value))
            {
                RebuildDisplayedEvidenceCards();
            }
        }
    }

    public bool PrioritizeAbnormal
    {
        get => _prioritizeAbnormal;
        set
        {
            if (SetProperty(ref _prioritizeAbnormal, value))
            {
                RebuildDisplayedEvidenceCards();
            }
        }
    }

    public bool PendingOnly
    {
        get => _pendingOnly;
        set
        {
            if (SetProperty(ref _pendingOnly, value))
            {
                RebuildDisplayedEvidenceCards();
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (SetProperty(ref _isRefreshing, value))
            {
                RaisePropertyChanged(nameof(CanRefresh));
            }
        }
    }

    public bool IsSavingManualReview
    {
        get => _isSavingManualReview;
        private set
        {
            if (SetProperty(ref _isSavingManualReview, value))
            {
                RaisePropertyChanged(nameof(CanSaveManualReview));
                RaisePropertyChanged(nameof(SaveManualReviewButtonText));
            }
        }
    }

    public bool HasSelectedDevice => _selectedScopeDevice is not null;

    public bool HasSelectedEvidence => _selectedEvidence is not null;

    public bool HasSelectedImage => _selectedEvidence?.HasImageUri == true;

    public bool CanRefresh => !IsRefreshing;

    public bool CanSaveManualReview =>
        HasSelectedEvidence &&
        !IsSavingManualReview &&
        SelectedConclusion?.Key is { Length: > 0 } &&
        !string.IsNullOrWhiteSpace(Reviewer);

    public bool IsComparisonWallMode => string.Equals(CurrentWallKey, WallModeComparison, StringComparison.OrdinalIgnoreCase);

    public bool HasDisplayedEvidenceCards => DisplayedEvidenceCards.Count > 0;

    public bool HasWallPlaceholder => IsComparisonWallMode || !HasDisplayedEvidenceCards;

    public int DisplayedEvidenceCount => DisplayedEvidenceCards.Count;

    public string CurrentSchemeText => _overview?.CurrentScheme.Name ?? "未加载巡检范围方案";

    public string SchemeSummaryText
    {
        get
        {
            if (_overview is null)
            {
                return "正在读取当前方案范围与点位覆盖情况。";
            }

            var summary = _overview.ScopeSummary;
            return $"覆盖 {summary.CoveredPointCount} 个点位 / 在线 {summary.OnlinePointCount} / 离线 {summary.OfflinePointCount} / 重点 {summary.FocusPointCount}";
        }
    }

    public string CurrentWallTitle => _selectedWallMode?.Title ?? "全点位截图墙";

    public string CurrentWallDescription => CurrentWallKey switch
    {
        WallModeAbnormal => "以异常样本为主，优先收敛画面异常、AI 告警和连续异常点位。",
        WallModeComparison => "预留同刻对照墙结构，后续接入同源截图并排对照与时间轴聚类。",
        _ => "汇总当前巡检方案内全部截图样本与 AI 证据图，用于人工快速浏览与复核。"
    };

    public string CurrentWallSummaryText
    {
        get
        {
            if (IsComparisonWallMode)
            {
                return "当前为结构预留模式，本轮先完成截图总览、异常优先墙与单点人工复核闭环。";
            }

            var filters = new List<string>();
            if (PrioritizeFocused)
            {
                filters.Add("重点点位优先");
            }

            if (PrioritizeAbnormal)
            {
                filters.Add("异常点位优先");
            }

            if (PendingOnly)
            {
                filters.Add("仅看待人工复核");
            }

            var filterText = filters.Count == 0
                ? "默认时间排序"
                : string.Join(" / ", filters);
            return $"当前展示 {DisplayedEvidenceCount} 张样本，{filterText}。";
        }
    }

    public string WallPlaceholderText => IsComparisonWallMode
        ? "同刻对照墙结构已预留。本轮先完成截图墙汇总、异常优先排序与人工复核写回。"
        : "当前方案与筛选条件下暂无可展示的截图样本。";

    public string LastUpdatedText => _overview?.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    public string SelectedDeviceName => _selectedEvidence?.DeviceName ?? _selectedScopeDevice?.Device.DeviceName ?? "未选择点位";

    public string SelectedDeviceCode => _selectedEvidence?.DeviceCode ?? _selectedScopeDevice?.Device.DeviceCode ?? "--";

    public string SelectedDeviceDirectoryText => FirstNonEmpty(_selectedEvidence?.DirectoryPath, _selectedScopeDevice?.Device.DirectoryPath, "--");

    public string SelectedDeviceStatusText => BuildSelectedDeviceStatusText(_selectedScopeDevice);

    public string SelectedEvidenceTimeText => _selectedEvidence?.CapturedAtText ?? "--";

    public string SelectedEvidenceSourceText => _selectedEvidence?.EvidenceKindText ?? "--";

    public string SelectedReviewSourceText => _selectedEvidence?.SourceKindText ?? "--";

    public string SelectedEvidenceRoleText => _selectedEvidence?.EvidenceRoleText ?? "--";

    public string SelectedEvidenceTagText => _selectedEvidence?.AbnormalTagText ?? "--";

    public string SelectedEvidencePlaybackGradeText => ResolveSelectedPlaybackGradeText();

    public string SelectedEvidenceManualConclusionText => _selectedEvidence?.ManualReviewConclusionText ?? "待复核";

    public string SelectedEvidenceManualReviewedAtText => _selectedEvidence?.ManualReviewedAtText ?? "--";

    public string SelectedEvidenceAssociationText => BuildSelectedEvidenceAssociationText(_selectedEvidence);

    public string SelectedEvidenceNoteText => BuildSelectedEvidenceNoteText(_selectedEvidence);

    public string SelectedImageUri => _selectedEvidence?.ImageUri ?? string.Empty;

    public string SelectedContextHintText
    {
        get
        {
            if (!HasSelectedDevice)
            {
                return "选择截图墙中的样本后，可在这里写回复核结论并联动直播或回看快速播放。";
            }

            if (HasSelectedEvidence)
            {
                return "已同步当前点位与选中样本，可直接写回复核结论并复用现有轻量播放器宿主。";
            }

            return "当前点位暂无已汇总证据样本，可先用下方播放器宿主加载直播或最近回看文件。";
        }
    }

    public string SelectedFilterNoticeText =>
        HasSelectedEvidence &&
        DisplayedEvidenceCards.All(card => !string.Equals(card.Evidence.EvidenceId, _selectedEvidence?.EvidenceId, StringComparison.OrdinalIgnoreCase))
            ? "当前选中的样本已被当前墙视图或筛选条件移出，但仍可继续复核与播放联动。"
            : string.Empty;

    public string SaveManualReviewButtonText => IsSavingManualReview ? "保存中..." : "写回复核结论";

    public ICommand RefreshCommand { get; }

    public ICommand RefreshAiAlertsCommand { get; }

    public ICommand SelectWallModeCommand { get; }

    public ICommand SelectEvidenceCommand { get; }

    public ICommand SaveManualReviewCommand { get; }

    public ICommand ResetReviewDraftCommand { get; }

    private string CurrentWallKey => _selectedWallMode?.Key ?? WallModeAll;

    private void OnScopeChanged(object? sender, EventArgs e)
    {
        if (_suppressScopeEvent)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = RefreshOverviewAsync(forceRefreshAiAlerts: false, preserveSelection: true);
        }));
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var deviceCode = _inspectionSelectionService.GetSelectedDeviceCode();
            if (!string.IsNullOrWhiteSpace(deviceCode))
            {
                TrySelectDevice(deviceCode, publishSelection: false);
            }
        }));
    }

    private async Task RefreshOverviewAsync(bool forceRefreshAiAlerts, bool preserveSelection)
    {
        if (IsRefreshing)
        {
            return;
        }

        var preferredEvidenceId = preserveSelection ? _selectedEvidence?.EvidenceId : null;
        var preferredDeviceCode = preserveSelection
            ? _selectedEvidence?.DeviceCode ?? _selectedScopeDevice?.Device.DeviceCode
            : _inspectionSelectionService.GetSelectedDeviceCode();

        IsRefreshing = true;
        StatusText = forceRefreshAiAlerts
            ? "正在刷新 AI 画面异常消息与证据图..."
            : "正在汇总当前方案内截图样本...";
        WarningText = string.Empty;

        try
        {
            var overview = await Task.Run(() => _reviewCenterService.GetOverview(new ReviewCenterQuery
            {
                ForceRefreshAiAlerts = forceRefreshAiAlerts,
                AiRecentDays = 7,
                AiMaxPages = 3,
                AiPageSize = 40
            }));
            var scopeResult = await Task.Run(() => _inspectionScopeService.GetCurrentScope());

            ApplyOverview(overview, scopeResult, preferredEvidenceId, preferredDeviceCode);
            StatusText = overview.StatusMessage;
            WarningText = overview.WarningMessage;
        }
        catch (Exception ex)
        {
            StatusText = forceRefreshAiAlerts
                ? "AI 画面异常刷新失败，已保留当前截图墙数据。"
                : "截图总览加载失败。";
            WarningText = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyOverview(
        ReviewCenterOverview overview,
        InspectionScopeResult scopeResult,
        string? preferredEvidenceId,
        string? preferredDeviceCode)
    {
        _overview = overview;
        _scopeResult = scopeResult;
        _scopeDeviceLookup = scopeResult.Devices
            .Where(item => !string.IsNullOrWhiteSpace(item.Device.DeviceCode))
            .GroupBy(item => item.Device.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        _allEvidenceItems = overview.EvidenceItems;

        RebuildSummaryCards(overview, scopeResult);
        RefreshSchemeItems(scopeResult.CurrentScheme.Id);
        RebuildDisplayedEvidenceCards();
        RestoreSelection(preferredEvidenceId, preferredDeviceCode);
        RefreshSourceTaskContext(_selectedEvidence?.DeviceCode ?? _selectedScopeDevice?.Device.DeviceCode);

        RaisePropertyChanged(nameof(CurrentSchemeText));
        RaisePropertyChanged(nameof(SchemeSummaryText));
        RaisePropertyChanged(nameof(LastUpdatedText));
    }

    private void RebuildSummaryCards(ReviewCenterOverview overview, InspectionScopeResult scopeResult)
    {
        ReplaceCollection(
            SummaryCards,
        [
            BuildMetric("方案覆盖", scopeResult.Summary.CoveredPointCount.ToString(), "点位", overview.CurrentScheme.Name, "TonePrimaryBrush"),
            BuildMetric("截图与证据", overview.TotalEvidenceCount.ToString(), "条", $"当前样本 {overview.EvidenceItems.Count(item => string.Equals(item.EvidenceRole, ReviewEvidenceRoles.Current, StringComparison.OrdinalIgnoreCase))}", "ToneInfoBrush"),
            BuildMetric("待人工复核", overview.PendingManualCount.ToString(), "条", $"异常样本 {overview.AbnormalEvidenceCount}", "ToneWarningBrush"),
            BuildMetric("AI 画面异常", overview.AiEvidenceCount.ToString(), "条", $"连续异常 {overview.ContinuousAbnormalPointCount} 个点位", "ToneDangerBrush"),
            BuildMetric("重点点位样本", overview.FocusedEvidenceCount.ToString(), "条", $"重点点位 {scopeResult.Summary.FocusPointCount}", "ToneFocusBrush")
        ]);
    }

    private void RefreshSchemeItems(string currentSchemeId)
    {
        var schemes = _inspectionScopeService.GetSchemes();

        _isUpdatingSchemeSelection = true;
        try
        {
            ReplaceCollection(
                SchemeItems,
                schemes.Select(scheme => new SelectionItemViewModel
                {
                    Key = scheme.Id,
                    Title = scheme.IsDefault ? $"{scheme.Name} / 默认" : scheme.Name,
                    Subtitle = string.IsNullOrWhiteSpace(scheme.Description)
                        ? BuildRuleSummary(scheme)
                        : scheme.Description,
                    IsSelected = string.Equals(scheme.Id, currentSchemeId, StringComparison.OrdinalIgnoreCase)
                }));
            SelectedScheme = SchemeItems.FirstOrDefault(item => item.IsSelected) ?? SchemeItems.FirstOrDefault();
        }
        finally
        {
            _isUpdatingSchemeSelection = false;
        }
    }

    private void RebuildDisplayedEvidenceCards()
    {
        IEnumerable<ReviewEvidenceItem> items = _allEvidenceItems;

        if (IsComparisonWallMode)
        {
            items = Array.Empty<ReviewEvidenceItem>();
        }
        else
        {
            if (PendingOnly)
            {
                items = items.Where(item => item.IsPendingManualReview);
            }

            if (string.Equals(CurrentWallKey, WallModeAbnormal, StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(IsAbnormalWallCandidate);
            }

            items = OrderEvidenceItems(items);
        }

        ReplaceCollection(
            DisplayedEvidenceCards,
            items.Select(item => new ReviewEvidenceCardViewModel(item)));
        UpdateCardSelection();

        RaisePropertyChanged(nameof(DisplayedEvidenceCount));
        RaisePropertyChanged(nameof(HasDisplayedEvidenceCards));
        RaisePropertyChanged(nameof(HasWallPlaceholder));
        RaisePropertyChanged(nameof(WallPlaceholderText));
        RaisePropertyChanged(nameof(CurrentWallTitle));
        RaisePropertyChanged(nameof(CurrentWallDescription));
        RaisePropertyChanged(nameof(CurrentWallSummaryText));
        RaisePropertyChanged(nameof(IsComparisonWallMode));
        RaisePropertyChanged(nameof(SelectedFilterNoticeText));
    }

    private IEnumerable<ReviewEvidenceItem> OrderEvidenceItems(IEnumerable<ReviewEvidenceItem> items)
    {
        return items
            .OrderByDescending(item => string.Equals(CurrentWallKey, WallModeAbnormal, StringComparison.OrdinalIgnoreCase) || PrioritizeAbnormal
                ? GetAbnormalPriority(item)
                : 0)
            .ThenByDescending(item => PrioritizeFocused && item.IsFocused)
            .ThenByDescending(item => string.Equals(item.EvidenceRole, ReviewEvidenceRoles.Current, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.CapturedAt)
            .ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase);
    }

    private void RestoreSelection(string? preferredEvidenceId, string? preferredDeviceCode)
    {
        if (!string.IsNullOrWhiteSpace(preferredEvidenceId))
        {
            var evidence = _allEvidenceItems.FirstOrDefault(item =>
                string.Equals(item.EvidenceId, preferredEvidenceId, StringComparison.OrdinalIgnoreCase));
            if (evidence is not null)
            {
                SelectEvidence(evidence, publishSelection: false);
                return;
            }
        }

        var candidateDeviceCode = FirstNonEmpty(preferredDeviceCode, _inspectionSelectionService.GetSelectedDeviceCode());
        if (!string.IsNullOrWhiteSpace(candidateDeviceCode) && TrySelectDevice(candidateDeviceCode, publishSelection: false))
        {
            return;
        }

        var firstEvidence = _allEvidenceItems.FirstOrDefault();
        if (firstEvidence is not null)
        {
            SelectEvidence(firstEvidence, publishSelection: false);
            return;
        }

        var firstScopeDevice = _scopeResult?.Devices.FirstOrDefault();
        if (firstScopeDevice is not null)
        {
            SelectScopeDevice(firstScopeDevice, publishSelection: false);
            return;
        }

        ClearSelectedContext();
    }

    private bool TrySelectDevice(string? deviceCode, bool publishSelection)
    {
        if (string.IsNullOrWhiteSpace(deviceCode) ||
            !_scopeDeviceLookup.TryGetValue(deviceCode, out var scopeDevice))
        {
            return false;
        }

        var evidence = _allEvidenceItems
            .Where(item => string.Equals(item.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => string.Equals(item.EvidenceRole, ReviewEvidenceRoles.Current, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(GetAbnormalPriority)
            .ThenByDescending(item => item.CapturedAt)
            .FirstOrDefault();

        if (evidence is not null)
        {
            SelectEvidence(evidence, publishSelection);
            return true;
        }

        SelectScopeDevice(scopeDevice, publishSelection);
        return true;
    }

    private void SelectWallMode(SelectionItemViewModel? item)
    {
        if (item is null || ReferenceEquals(item, _selectedWallMode))
        {
            return;
        }

        foreach (var mode in WallModeItems)
        {
            mode.IsSelected = ReferenceEquals(mode, item);
        }

        _selectedWallMode = item;
        RebuildDisplayedEvidenceCards();
    }

    private void SelectEvidence(ReviewEvidenceItem? evidence, bool publishSelection)
    {
        if (evidence is null)
        {
            ClearSelectedContext();
            return;
        }

        _scopeDeviceLookup.TryGetValue(evidence.DeviceCode, out var scopeDevice);
        ApplySelectedContext(scopeDevice, evidence, publishSelection);
    }

    private void SelectScopeDevice(InspectionScopeDevice? scopeDevice, bool publishSelection)
    {
        ApplySelectedContext(scopeDevice, null, publishSelection);
    }

    private void ApplySelectedContext(InspectionScopeDevice? scopeDevice, ReviewEvidenceItem? evidence, bool publishSelection)
    {
        _selectedScopeDevice = scopeDevice;
        _selectedEvidence = evidence;

        UpdateCardSelection();
        ResetReviewDraft();
        SyncMediaReviewContext();
        RefreshSourceTaskContext(scopeDevice?.Device.DeviceCode ?? evidence?.DeviceCode);
        RaiseSelectedContextChanged();

        if (publishSelection)
        {
            PublishSelectedDevice(scopeDevice?.Device.DeviceCode ?? evidence?.DeviceCode);
        }
    }

    private void ClearSelectedContext()
    {
        _selectedScopeDevice = null;
        _selectedEvidence = null;
        UpdateCardSelection();
        ResetReviewDraft();
        SyncMediaReviewContext();
        RefreshSourceTaskContext(null);
        RaiseSelectedContextChanged();
    }

    private void RaiseSelectedContextChanged()
    {
        RaisePropertyChanged(nameof(SelectedEvidence));
        RaisePropertyChanged(nameof(HasSelectedDevice));
        RaisePropertyChanged(nameof(HasSelectedEvidence));
        RaisePropertyChanged(nameof(HasSelectedImage));
        RaisePropertyChanged(nameof(SelectedDeviceName));
        RaisePropertyChanged(nameof(SelectedDeviceCode));
        RaisePropertyChanged(nameof(SelectedDeviceDirectoryText));
        RaisePropertyChanged(nameof(SelectedDeviceStatusText));
        RaisePropertyChanged(nameof(SelectedEvidenceTimeText));
        RaisePropertyChanged(nameof(SelectedEvidenceSourceText));
        RaisePropertyChanged(nameof(SelectedReviewSourceText));
        RaisePropertyChanged(nameof(SelectedEvidenceRoleText));
        RaisePropertyChanged(nameof(SelectedEvidenceTagText));
        RaisePropertyChanged(nameof(SelectedEvidencePlaybackGradeText));
        RaisePropertyChanged(nameof(SelectedEvidenceManualConclusionText));
        RaisePropertyChanged(nameof(SelectedEvidenceManualReviewedAtText));
        RaisePropertyChanged(nameof(SelectedEvidenceAssociationText));
        RaisePropertyChanged(nameof(SelectedEvidenceNoteText));
        RaisePropertyChanged(nameof(SelectedImageUri));
        RaisePropertyChanged(nameof(SelectedContextHintText));
        RaisePropertyChanged(nameof(SelectedFilterNoticeText));
        RaisePropertyChanged(nameof(CanSaveManualReview));
    }

    private void UpdateCardSelection()
    {
        var selectedEvidenceId = _selectedEvidence?.EvidenceId;
        foreach (var card in DisplayedEvidenceCards)
        {
            card.IsSelected = string.Equals(card.Evidence.EvidenceId, selectedEvidenceId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ResetReviewDraft()
    {
        SaveErrorText = string.Empty;

        if (_selectedEvidence is null)
        {
            SelectedConclusion = null;
            Reviewer = ResolveDefaultReviewer();
            ReviewRemark = string.Empty;
            RequiresDispatch = false;
            RequiresRecheck = _selectedScopeDevice?.NeedRecheck ?? false;
            SaveStatusText = HasSelectedDevice
                ? "当前点位暂无已汇总证据，可先联动直播或回看快速播放。"
                : "选择截图样本后可写回复核结论。";
            return;
        }

        Reviewer = string.IsNullOrWhiteSpace(_selectedEvidence.ManualReviewReviewer)
            ? ResolveDefaultReviewer()
            : _selectedEvidence.ManualReviewReviewer;
        ReviewRemark = _selectedEvidence.ManualReviewRemark;
        RequiresDispatch = _selectedEvidence.RequiresDispatch;
        RequiresRecheck = _selectedEvidence.RequiresRecheck || _selectedEvidence.NeedRecheck;
        SelectedConclusion = string.Equals(_selectedEvidence.ManualReviewConclusion, ManualReviewConclusions.Pending, StringComparison.OrdinalIgnoreCase)
            ? null
            : ConclusionItems.FirstOrDefault(item =>
                string.Equals(item.Key, _selectedEvidence.ManualReviewConclusion, StringComparison.OrdinalIgnoreCase));
        SaveStatusText = _selectedEvidence.ManualReviewedAt.HasValue
            ? $"最近人工复核：{_selectedEvidence.ManualReviewConclusionText} / {FirstNonEmpty(_selectedEvidence.ManualReviewReviewer, "未署名")} / {_selectedEvidence.ManualReviewedAtText}"
            : "当前证据尚未写回复核结论。";
    }

    private void SyncMediaReviewContext()
    {
        var scopeDevice = _selectedScopeDevice;
        if (scopeDevice is null && _selectedEvidence is not null)
        {
            _scopeDeviceLookup.TryGetValue(_selectedEvidence.DeviceCode, out scopeDevice);
        }

        if (scopeDevice is null)
        {
            _mediaReview.Clear();
            return;
        }

        _mediaReview.BindTarget(
            scopeDevice.Device.DeviceCode,
            scopeDevice.Device.DeviceName,
            scopeDevice.Device.NetTypeCode,
            scopeDevice.LatestInspection);
    }

    private async Task SwitchSchemeAsync(string schemeId)
    {
        if (string.IsNullOrWhiteSpace(schemeId) ||
            string.Equals(_overview?.CurrentScheme.Id, schemeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _suppressScopeEvent = true;
        try
        {
            await Task.Run(() => _inspectionScopeService.SetCurrentScheme(schemeId));
        }
        finally
        {
            _suppressScopeEvent = false;
        }

        await RefreshOverviewAsync(forceRefreshAiAlerts: false, preserveSelection: true);
    }

    private async Task SaveManualReviewAsync()
    {
        if (_selectedEvidence is null)
        {
            SaveErrorText = "请选择截图样本或 AI 证据后再写回复核结论。";
            return;
        }

        if (SelectedConclusion?.Key is not { Length: > 0 } conclusion)
        {
            SaveErrorText = "请选择人工复核结论。";
            return;
        }

        if (string.IsNullOrWhiteSpace(Reviewer))
        {
            SaveErrorText = "请填写复核人。";
            return;
        }

        var evidence = _selectedEvidence;
        IsSavingManualReview = true;
        SaveErrorText = string.Empty;
        SaveStatusText = "正在写回复核结论...";

        try
        {
            var record = await Task.Run(() => _reviewCenterService.SaveManualReview(new ManualReviewSaveRequest
            {
                EvidenceId = evidence.EvidenceId,
                DeviceCode = evidence.DeviceCode,
                DeviceName = evidence.DeviceName,
                SchemeId = evidence.SchemeId,
                SchemeName = evidence.SchemeName,
                SourceKind = evidence.SourceKind,
                Conclusion = conclusion,
                Reviewer = Reviewer.Trim(),
                RemarkText = ReviewRemark.Trim(),
                RequiresDispatch = RequiresDispatch,
                RequiresRecheck = RequiresRecheck,
                RelatedScreenshotSampleId = string.Equals(evidence.EvidenceKind, ReviewEvidenceKinds.AiEvidence, StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : evidence.EvidenceId,
                RelatedPlaybackReviewSessionId = evidence.ReviewSessionId,
                RelatedAiAlertId = evidence.AiAlertId,
                RelatedDeviceCode = evidence.DeviceCode
            }));

            SaveStatusText = $"已写回：{record.ConclusionText} / {record.ReviewedAtText}";
            await RefreshOverviewAsync(forceRefreshAiAlerts: false, preserveSelection: true);
        }
        catch (Exception ex)
        {
            SaveStatusText = "人工复核保存失败。";
            SaveErrorText = ex.Message;
        }
        finally
        {
            IsSavingManualReview = false;
        }
    }

    private void PublishSelectedDevice(string? deviceCode)
    {
        if (_suppressSelectionSync)
        {
            return;
        }

        _suppressSelectionSync = true;
        try
        {
            _inspectionSelectionService.SetSelectedDevice(deviceCode);
        }
        finally
        {
            _suppressSelectionSync = false;
        }
    }

    private string ResolveSelectedPlaybackGradeText()
    {
        if (_selectedEvidence is not null)
        {
            return _selectedEvidence.PlaybackGradeText;
        }

        if (_selectedScopeDevice?.LatestInspection is not null)
        {
            return _selectedScopeDevice.LatestInspection.PlaybackHealthSummary;
        }

        return _selectedScopeDevice?.PlaybackHealthGrade is { } grade
            ? $"等级 {grade}"
            : "--";
    }

    private static bool IsAbnormalWallCandidate(ReviewEvidenceItem item)
    {
        return item.IsPendingManualReview ||
               item.IsContinuousAbnormal ||
               item.IsAbnormal ||
               string.Equals(item.EvidenceKind, ReviewEvidenceKinds.AiEvidence, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetAbnormalPriority(ReviewEvidenceItem item)
    {
        var score = 0;
        if (item.IsPendingManualReview)
        {
            score += 80;
        }

        if (item.IsContinuousAbnormal)
        {
            score += 60;
        }

        if (item.IsAbnormal)
        {
            score += 40;
        }

        if (string.Equals(item.EvidenceKind, ReviewEvidenceKinds.AiEvidence, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (item.NeedRecheck)
        {
            score += 10;
        }

        return score;
    }

    private static string BuildSelectedDeviceStatusText(InspectionScopeDevice? scopeDevice)
    {
        if (scopeDevice is null)
        {
            return "--";
        }

        var segments = new List<string>();
        if (scopeDevice.IsFocused)
        {
            segments.Add("重点关注");
        }

        segments.Add(scopeDevice.IsOnline ? "在线" : "离线");

        if (scopeDevice.PlaybackHealthGrade is { } grade)
        {
            segments.Add($"等级 {grade}");
        }

        if (scopeDevice.NeedRecheck)
        {
            segments.Add("需复检");
        }

        return string.Join(" / ", segments);
    }

    private static string BuildSelectedEvidenceAssociationText(ReviewEvidenceItem? evidence)
    {
        if (evidence is null)
        {
            return "--";
        }

        return FirstNonEmpty(
            evidence.PlaybackFileName,
            evidence.AiAlertSourceName,
            evidence.Protocol,
            "--");
    }

    private static string BuildSelectedEvidenceNoteText(ReviewEvidenceItem? evidence)
    {
        if (evidence is null)
        {
            return "选择截图样本后，这里展示 AI 告警描述、基础巡检异常信息和人工复核备注。";
        }

        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(evidence.AiAlertContent))
        {
            segments.Add(evidence.AiAlertContent.Trim());
        }

        if (!string.IsNullOrWhiteSpace(evidence.FailureReason))
        {
            segments.Add(evidence.FailureReason.Trim());
        }

        if (!string.IsNullOrWhiteSpace(evidence.ManualReviewRemark))
        {
            segments.Add($"复核备注：{evidence.ManualReviewRemark.Trim()}");
        }

        return segments.Count == 0
            ? "当前样本暂无补充说明，可直接进入直播或回看快速播放。"
            : string.Join(Environment.NewLine, segments);
    }

    private static SelectionItemViewModel BuildConclusionItem(string key, string title, string subtitle)
    {
        return new SelectionItemViewModel
        {
            Key = key,
            Title = title,
            Subtitle = subtitle
        };
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

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string ResolveDefaultReviewer()
    {
        return string.IsNullOrWhiteSpace(Environment.UserName)
            ? "当前值班人"
            : Environment.UserName.Trim();
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
