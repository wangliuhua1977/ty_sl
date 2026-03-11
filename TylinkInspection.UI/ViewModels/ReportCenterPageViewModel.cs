using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class ReportCenterPageViewModel : PageViewModelBase
{
    private readonly IReportCenterService _reportCenterService;
    private readonly IInspectionScopeService _inspectionScopeService;

    private SelectionItemViewModel? _selectedTimeRangeOption;
    private string _statusText = "正在汇总本地报表统计...";
    private string _warningText = string.Empty;
    private string _lastUpdatedText = "--";
    private string _currentSchemeText = "当前巡检范围方案未加载";
    private string _activeRangeText = "--";
    private string _reportKindText = "--";
    private string _inspectionSummaryText = "等待统计结果";
    private string _reviewSummaryText = "等待统计结果";
    private string _faultSummaryText = "等待统计结果";
    private string _recheckSummaryText = "等待统计结果";
    private string _exportReserveText = "尚未生成导出快照。";
    private string _boundaryText = "当前周期与方案范围尚未加载。";
    private DateTime? _customStartDate = DateTime.Now.Date.AddDays(-6);
    private DateTime? _customEndDate = DateTime.Now.Date;
    private bool _isRefreshing;

    public ReportCenterPageViewModel(
        ModulePageData pageData,
        IReportCenterService reportCenterService,
        IInspectionScopeService inspectionScopeService)
        : base(pageData.PageTitle, pageData.PageSubtitle)
    {
        _reportCenterService = reportCenterService;
        _inspectionScopeService = inspectionScopeService;
        _inspectionScopeService.ScopeChanged += OnScopeChanged;

        StatusBadgeText = pageData.StatusBadgeText;
        StatusBadgeAccentResourceKey = pageData.StatusBadgeAccentResourceKey;

        TimeRangeOptions = new ObservableCollection<SelectionItemViewModel>(
        [
            new SelectionItemViewModel
            {
                Key = ReportTimeRangePresets.Today,
                Title = "今日",
                Subtitle = "日报口径",
                Badge = "D",
                IsSelected = true
            },
            new SelectionItemViewModel
            {
                Key = ReportTimeRangePresets.Last7Days,
                Title = "最近7天",
                Subtitle = "周报口径",
                Badge = "W"
            },
            new SelectionItemViewModel
            {
                Key = ReportTimeRangePresets.Last30Days,
                Title = "最近30天",
                Subtitle = "月报口径",
                Badge = "M"
            },
            new SelectionItemViewModel
            {
                Key = ReportTimeRangePresets.Custom,
                Title = "自定义",
                Subtitle = "区间口径",
                Badge = "C"
            }
        ]);
        _selectedTimeRangeOption = TimeRangeOptions.FirstOrDefault(item => item.IsSelected) ?? TimeRangeOptions.FirstOrDefault();

        OverviewCards = new ObservableCollection<OverviewMetric>();
        InspectionCards = new ObservableCollection<OverviewMetric>();
        ReviewCards = new ObservableCollection<OverviewMetric>();
        FaultCards = new ObservableCollection<OverviewMetric>();
        RecheckCards = new ObservableCollection<OverviewMetric>();
        InspectionGradeItems = new ObservableCollection<ReportVisualItemViewModel>();
        InspectionTrendItems = new ObservableCollection<ReportVisualItemViewModel>();
        ReviewConclusionItems = new ObservableCollection<ReportVisualItemViewModel>();
        ReviewSourceItems = new ObservableCollection<ReportVisualItemViewModel>();
        ReviewTrendItems = new ObservableCollection<ReportVisualItemViewModel>();
        FaultStatusItems = new ObservableCollection<ReportVisualItemViewModel>();
        FaultSourceItems = new ObservableCollection<ReportVisualItemViewModel>();
        FaultTrendItems = new ObservableCollection<ReportVisualItemViewModel>();
        RecheckOutcomeItems = new ObservableCollection<ReportVisualItemViewModel>();
        RecheckTriggerItems = new ObservableCollection<ReportVisualItemViewModel>();
        RecheckTrendItems = new ObservableCollection<ReportVisualItemViewModel>();

        SelectTimeRangeCommand = new RelayCommand<SelectionItemViewModel>(SelectTimeRange);
        RefreshCommand = new RelayCommand<object?>(_ => _ = RefreshAsync());
        ApplyCustomRangeCommand = new RelayCommand<object?>(_ => _ = ApplyCustomRangeAsync());

        _ = RefreshAsync();
    }

    public string StatusBadgeText { get; }

    public string StatusBadgeAccentResourceKey { get; }

    public ObservableCollection<SelectionItemViewModel> TimeRangeOptions { get; }

    public ObservableCollection<OverviewMetric> OverviewCards { get; }

    public ObservableCollection<OverviewMetric> InspectionCards { get; }

    public ObservableCollection<OverviewMetric> ReviewCards { get; }

    public ObservableCollection<OverviewMetric> FaultCards { get; }

    public ObservableCollection<OverviewMetric> RecheckCards { get; }

    public ObservableCollection<ReportVisualItemViewModel> InspectionGradeItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> InspectionTrendItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> ReviewConclusionItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> ReviewSourceItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> ReviewTrendItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> FaultStatusItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> FaultSourceItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> FaultTrendItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> RecheckOutcomeItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> RecheckTriggerItems { get; }

    public ObservableCollection<ReportVisualItemViewModel> RecheckTrendItems { get; }

    public DateTime? CustomStartDate
    {
        get => _customStartDate;
        set => SetProperty(ref _customStartDate, value);
    }

    public DateTime? CustomEndDate
    {
        get => _customEndDate;
        set => SetProperty(ref _customEndDate, value);
    }

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

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string CurrentSchemeText
    {
        get => _currentSchemeText;
        private set => SetProperty(ref _currentSchemeText, value);
    }

    public string ActiveRangeText
    {
        get => _activeRangeText;
        private set => SetProperty(ref _activeRangeText, value);
    }

    public string ReportKindText
    {
        get => _reportKindText;
        private set => SetProperty(ref _reportKindText, value);
    }

    public string InspectionSummaryText
    {
        get => _inspectionSummaryText;
        private set => SetProperty(ref _inspectionSummaryText, value);
    }

    public string ReviewSummaryText
    {
        get => _reviewSummaryText;
        private set => SetProperty(ref _reviewSummaryText, value);
    }

    public string FaultSummaryText
    {
        get => _faultSummaryText;
        private set => SetProperty(ref _faultSummaryText, value);
    }

    public string RecheckSummaryText
    {
        get => _recheckSummaryText;
        private set => SetProperty(ref _recheckSummaryText, value);
    }

    public string ExportReserveText
    {
        get => _exportReserveText;
        private set => SetProperty(ref _exportReserveText, value);
    }

    public string BoundaryText
    {
        get => _boundaryText;
        private set => SetProperty(ref _boundaryText, value);
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

    public bool CanRefresh => !IsRefreshing;

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);

    public bool IsCustomRangeSelected => string.Equals(_selectedTimeRangeOption?.Key, ReportTimeRangePresets.Custom, StringComparison.OrdinalIgnoreCase);

    public ICommand SelectTimeRangeCommand { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ApplyCustomRangeCommand { get; }

    private void OnScopeChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = RefreshAsync();
        }));
    }

    private void SelectTimeRange(SelectionItemViewModel? item)
    {
        if (item is null || ReferenceEquals(item, _selectedTimeRangeOption))
        {
            return;
        }

        foreach (var option in TimeRangeOptions)
        {
            option.IsSelected = ReferenceEquals(option, item);
        }

        _selectedTimeRangeOption = item;
        RaisePropertyChanged(nameof(IsCustomRangeSelected));

        if (!string.Equals(item.Key, ReportTimeRangePresets.Custom, StringComparison.OrdinalIgnoreCase))
        {
            _ = RefreshAsync();
        }
    }

    private async Task ApplyCustomRangeAsync()
    {
        if (!IsCustomRangeSelected)
        {
            return;
        }

        if (!CustomStartDate.HasValue || !CustomEndDate.HasValue)
        {
            WarningText = "请选择完整的自定义起止日期。";
            RaisePropertyChanged(nameof(HasWarning));
            return;
        }

        if (CustomEndDate.Value.Date < CustomStartDate.Value.Date)
        {
            WarningText = "自定义结束日期不能早于开始日期。";
            RaisePropertyChanged(nameof(HasWarning));
            return;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        ReportTimeRange targetRange;
        try
        {
            targetRange = BuildSelectedTimeRange();
        }
        catch (Exception ex)
        {
            WarningText = ex.Message;
            RaisePropertyChanged(nameof(HasWarning));
            return;
        }

        IsRefreshing = true;
        WarningText = string.Empty;
        RaisePropertyChanged(nameof(HasWarning));
        StatusText = $"正在按“{targetRange.Label}”统计巡检、复核、闭环与复检数据...";

        try
        {
            var overview = await Task.Run(() => _reportCenterService.GetOverview(targetRange));
            ApplyOverview(overview);
        }
        catch (Exception ex)
        {
            StatusText = "报表中心刷新失败。";
            WarningText = ex.Message;
            RaisePropertyChanged(nameof(HasWarning));
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private ReportTimeRange BuildSelectedTimeRange()
    {
        var key = _selectedTimeRangeOption?.Key ?? ReportTimeRangePresets.Today;
        return key switch
        {
            ReportTimeRangePresets.Last7Days => ReportTimeRange.CreateLast7Days(),
            ReportTimeRangePresets.Last30Days => ReportTimeRange.CreateLast30Days(),
            ReportTimeRangePresets.Custom => ReportTimeRange.CreateCustom(
                CustomStartDate ?? DateTime.Now.Date,
                CustomEndDate ?? DateTime.Now.Date),
            _ => ReportTimeRange.CreateToday()
        };
    }

    private void ApplyOverview(ReportOverview overview)
    {
        CurrentSchemeText = $"{overview.CurrentScheme.Name} / 覆盖 {overview.ScopeSummary.CoveredPointCount} 点位";
        ActiveRangeText = overview.TimeRange.DisplayText;
        ReportKindText = overview.ExportModel.ReportTitle;
        StatusText = overview.StatusMessage;
        WarningText = overview.WarningMessage;
        LastUpdatedText = overview.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        InspectionSummaryText = overview.Inspection.SummaryText;
        ReviewSummaryText = overview.Review.SummaryText;
        FaultSummaryText = overview.FaultClosure.SummaryText;
        RecheckSummaryText = overview.Recheck.SummaryText;
        ExportReserveText = $"已预留统一导出快照结构 {overview.ExportModel.SchemaVersion}，当前可直接承载 {overview.ExportModel.ReportTitle}、时间范围、方案信息与四大统计分区。";
        BoundaryText = $"基础巡检统计按当前方案内已沉淀的最新本地巡检结果计算；闭环状态和复检任务展示当前快照，趋势与周期统计分别按更新时间、执行完成时间过滤。";
        RaisePropertyChanged(nameof(HasWarning));

        ReplaceCollection(OverviewCards,
        [
            BuildMetric("巡检总数", overview.InspectionTotalCount.ToString(), "条", "当前周期基础巡检结果", "TonePrimaryBrush"),
            BuildMetric("在线点位", overview.OnlinePointCount.ToString(), "个", "按巡检结果统计", "ToneSuccessBrush"),
            BuildMetric("离线点位", overview.OfflinePointCount.ToString(), "个", "按巡检结果统计", "ToneDangerBrush"),
            BuildMetric("异常点位", overview.AbnormalPointCount.ToString(), "个", "需重点关注", "ToneWarningBrush"),
            BuildMetric("人工复核", overview.ManualReviewCount.ToString(), "条", "当前周期人工写回", "ToneInfoBrush"),
            BuildMetric("待派单/待复检", $"{overview.PendingDispatchCount}/{overview.PendingRecheckCount}", "条", "闭环快照", "ToneFocusBrush"),
            BuildMetric("复检成功率", $"{overview.RecheckSuccessRate:P1}", string.Empty, "按执行记录口径", "ToneSuccessBrush")
        ]);

        ReplaceCollection(InspectionCards,
        [
            BuildMetric("覆盖点位", overview.Inspection.CoveredPointCount.ToString(), "个", "当前方案范围", "ToneInfoBrush"),
            BuildMetric("巡检结果", overview.Inspection.TotalCount.ToString(), "条", "当前周期", "TonePrimaryBrush"),
            BuildMetric("待补巡检", overview.Inspection.MissingInspectionCount.ToString(), "个", "方案内暂无结果", "ToneWarningBrush"),
            BuildMetric("需复检", overview.Inspection.NeedRecheckCount.ToString(), "条", "按基础巡检判断", "ToneDangerBrush")
        ]);
        ReplaceCollection(ReviewCards,
        [
            BuildMetric("播放复核", overview.Review.PlaybackReviewSessionCount.ToString(), "场", "当前周期", "TonePrimaryBrush"),
            BuildMetric("截图样本", overview.Review.ScreenshotSampleCount.ToString(), "条", "用于人工复核", "ToneInfoBrush"),
            BuildMetric("人工复核", overview.Review.ManualReviewCount.ToString(), "条", "当前周期写回", "ToneSuccessBrush"),
            BuildMetric("待复核", overview.Review.PendingManualReviewCount.ToString(), "条", "结论仍待确认", "ToneWarningBrush")
        ]);
        ReplaceCollection(FaultCards,
        [
            BuildMetric("闭环记录", overview.FaultClosure.CurrentRecordCount.ToString(), "条", "当前方案累计", "TonePrimaryBrush"),
            BuildMetric("待派单", overview.FaultClosure.PendingDispatchCount.ToString(), "条", "当前快照", "ToneWarningBrush"),
            BuildMetric("待销警", overview.FaultClosure.PendingClearCount.ToString(), "条", "复检通过待确认", "ToneFocusBrush"),
            BuildMetric("已关闭/销警", $"{overview.FaultClosure.ClosedCount + overview.FaultClosure.ClearedCount + overview.FaultClosure.FalsePositiveClosedCount}", "条", "终态沉淀", "ToneSuccessBrush")
        ]);
        ReplaceCollection(RecheckCards,
        [
            BuildMetric("复检任务", overview.Recheck.TaskCount.ToString(), "条", "当前纳管", "TonePrimaryBrush"),
            BuildMetric("启用任务", overview.Recheck.EnabledTaskCount.ToString(), "条", "当前快照", "ToneInfoBrush"),
            BuildMetric("周期执行", overview.Recheck.ExecutionCount.ToString(), "次", "按执行记录统计", "ToneFocusBrush"),
            BuildMetric("失败率", $"{overview.Recheck.FailureRate:P1}", string.Empty, "失败/异常/取消", "ToneDangerBrush")
        ]);

        ReplaceCollection(InspectionGradeItems, BuildDistributionItems(overview.Inspection.PlaybackGradeDistribution, ResolveInspectionAccent));
        ReplaceCollection(InspectionTrendItems, BuildTrendItems(overview.Inspection.TrendPoints, "TonePrimaryBrush"));
        ReplaceCollection(ReviewConclusionItems, BuildDistributionItems(overview.Review.ConclusionDistribution, ResolveReviewAccent));
        ReplaceCollection(ReviewSourceItems, BuildDistributionItems(overview.Review.SourceDistribution, ResolveReviewSourceAccent));
        ReplaceCollection(ReviewTrendItems, BuildTrendItems(overview.Review.TrendPoints, "ToneInfoBrush"));
        ReplaceCollection(FaultStatusItems, BuildDistributionItems(overview.FaultClosure.StatusDistribution, ResolveFaultAccent));
        ReplaceCollection(FaultSourceItems, BuildDistributionItems(overview.FaultClosure.SourceDistribution, ResolveFaultSourceAccent));
        ReplaceCollection(FaultTrendItems, BuildTrendItems(overview.FaultClosure.TrendPoints, "ToneWarningBrush"));
        ReplaceCollection(RecheckOutcomeItems, BuildDistributionItems(overview.Recheck.OutcomeDistribution, ResolveRecheckOutcomeAccent));
        ReplaceCollection(RecheckTriggerItems, BuildDistributionItems(overview.Recheck.TriggerDistribution, ResolveRecheckTriggerAccent));
        ReplaceCollection(RecheckTrendItems, BuildTrendItems(overview.Recheck.TrendPoints, "ToneFocusBrush"));
    }

    private static IEnumerable<ReportVisualItemViewModel> BuildDistributionItems(
        IReadOnlyList<ReportCountSegment> segments,
        Func<string, string> accentResolver)
    {
        var max = Math.Max(1, segments.Max(item => item.Count));
        return segments.Select(item => new ReportVisualItemViewModel
        {
            Label = item.Label,
            ValueText = item.Count.ToString(),
            DetailText = item.DetailText,
            AccentResourceKey = accentResolver(item.Key),
            BarWidth = item.Count == 0 ? 16 : 28 + 132d * item.Count / max
        });
    }

    private static IEnumerable<ReportVisualItemViewModel> BuildTrendItems(
        IReadOnlyList<ReportTrendPoint> points,
        string accentResourceKey)
    {
        var max = Math.Max(1, points.Max(item => item.Value));
        return points.Select(item => new ReportVisualItemViewModel
        {
            Label = item.Label,
            ValueText = item.Value.ToString(),
            DetailText = item.DetailText,
            AccentResourceKey = accentResourceKey,
            BarWidth = item.Value == 0 ? 16 : 28 + 132d * item.Value / max
        });
    }

    private static string ResolveInspectionAccent(string key)
    {
        return key switch
        {
            "A" => "ToneSuccessBrush",
            "B" => "TonePrimaryBrush",
            "C" => "ToneInfoBrush",
            "D" => "ToneWarningBrush",
            "E" => "ToneDangerBrush",
            _ => "ToneInfoBrush"
        };
    }

    private static string ResolveReviewAccent(string key)
    {
        return key switch
        {
            ManualReviewConclusions.Pending => "ToneInfoBrush",
            ManualReviewConclusions.Normal => "ToneSuccessBrush",
            ManualReviewConclusions.FalsePositive => "ToneFocusBrush",
            _ => "ToneWarningBrush"
        };
    }

    private static string ResolveReviewSourceAccent(string key)
    {
        return key switch
        {
            ManualReviewSourceKinds.Live => "TonePrimaryBrush",
            ManualReviewSourceKinds.Playback => "ToneInfoBrush",
            ManualReviewSourceKinds.Ai => "ToneWarningBrush",
            _ => "ToneInfoBrush"
        };
    }

    private static string ResolveFaultAccent(string key)
    {
        return key switch
        {
            FaultClosureStatuses.PendingDispatch => "ToneWarningBrush",
            FaultClosureStatuses.PendingRecheck => "ToneDangerBrush",
            FaultClosureStatuses.RecheckPassedPendingClear => "ToneFocusBrush",
            FaultClosureStatuses.Cleared => "ToneSuccessBrush",
            FaultClosureStatuses.Closed => "ToneSuccessBrush",
            FaultClosureStatuses.FalsePositiveClosed => "ToneInfoBrush",
            _ => "ToneInfoBrush"
        };
    }

    private static string ResolveFaultSourceAccent(string key)
    {
        return key switch
        {
            FaultClosureSourceTypes.LiveReview => "TonePrimaryBrush",
            FaultClosureSourceTypes.PlaybackReview => "ToneInfoBrush",
            FaultClosureSourceTypes.AiAlert => "ToneWarningBrush",
            FaultClosureSourceTypes.InspectionFailure => "ToneDangerBrush",
            _ => "ToneInfoBrush"
        };
    }

    private static string ResolveRecheckOutcomeAccent(string key)
    {
        return key switch
        {
            RecheckExecutionOutcomes.Passed => "ToneSuccessBrush",
            RecheckExecutionOutcomes.Completed => "ToneFocusBrush",
            RecheckExecutionOutcomes.Failed => "ToneDangerBrush",
            RecheckExecutionOutcomes.Error => "ToneWarningBrush",
            RecheckExecutionOutcomes.Canceled => "ToneInfoBrush",
            _ => "ToneInfoBrush"
        };
    }

    private static string ResolveRecheckTriggerAccent(string key)
    {
        return key switch
        {
            RecheckExecutionTriggerTypes.Scheduled => "TonePrimaryBrush",
            RecheckExecutionTriggerTypes.Manual => "ToneInfoBrush",
            RecheckExecutionTriggerTypes.Retry => "ToneWarningBrush",
            RecheckExecutionTriggerTypes.Recovery => "ToneFocusBrush",
            _ => "ToneInfoBrush"
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

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
