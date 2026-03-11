using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class FaultClosureCenterPageViewModel : PageViewModelBase
{
    private readonly IFaultClosureService _faultClosureService;
    private readonly IRecheckSchedulerService _recheckSchedulerService;
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly DeviceMediaReviewViewModel _mediaReview;

    private SelectionItemViewModel? _selectedStatusOption;
    private SelectionItemViewModel? _selectedSourceOption;
    private SelectionItemViewModel? _selectedFaultTypeOption;
    private SelectionItemViewModel? _selectedFaultTypeRuleOption;
    private FaultClosureRecord? _selectedRecord;
    private RecheckTaskRecord? _selectedRecheckTask;
    private RecheckRuleCatalog _currentRuleCatalog = RecheckRuleCatalog.CreateDefault();
    private RecheckScheduleRule _currentRecheckRule = RecheckScheduleRule.CreateDefault();
    private string _statusText = "正在汇聚本地闭环记录...";
    private string _warningText = string.Empty;
    private string _lastUpdatedText = "--";
    private string _recheckRuleInitialDelayMinutesText = "5";
    private string _recheckRuleIntervalMinutesText = "30";
    private string _recheckRuleRetryDelayMinutesText = "5";
    private string _recheckRuleMaxRetryCountText = "3";
    private string _recheckRuleFeedbackText = string.Empty;
    private string _faultTypeRuleInitialDelayMinutesText = "5";
    private string _faultTypeRuleIntervalMinutesText = "30";
    private string _faultTypeRuleRetryDelayMinutesText = "5";
    private string _faultTypeRuleMaxRetryCountText = "3";
    private string _faultTypeRuleFeedbackText = string.Empty;
    private string _selectedTaskOverrideInitialDelayMinutesText = "5";
    private string _selectedTaskOverrideIntervalMinutesText = "30";
    private string _selectedTaskOverrideRetryDelayMinutesText = "5";
    private string _selectedTaskOverrideMaxRetryCountText = "3";
    private string _selectedTaskOverrideFeedbackText = string.Empty;
    private string _recheckQueueStatusText = "正在加载本地复检任务...";
    private string _operatorName = ResolveDefaultOperator();
    private string _actionNote = string.Empty;
    private bool _recheckRuleAutoEnabled = true;
    private bool _recheckRuleAllowManualOverride = true;
    private bool _faultTypeRuleAutoEnabled = true;
    private bool _selectedTaskOverrideAutoEnabled = true;
    private bool _selectedTaskOverrideEnabled;
    private bool _hasInitializedRuleEditor;
    private bool _pendingRecheckOnly;
    private bool _focusedOnly;
    private bool _isRefreshing;
    private bool _isProcessingAction;

    public FaultClosureCenterPageViewModel(
        ModulePageData pageData,
        IFaultClosureService faultClosureService,
        IRecheckSchedulerService recheckSchedulerService,
        IInspectionScopeService inspectionScopeService,
        IDeviceCatalogService deviceCatalogService,
        IDeviceInspectionService deviceInspectionService,
        IPlaybackReviewService playbackReviewService,
        IScreenshotSamplingService screenshotSamplingService,
        ICloudPlaybackService cloudPlaybackService)
        : base(pageData.PageTitle, pageData.PageSubtitle)
    {
        _faultClosureService = faultClosureService;
        _recheckSchedulerService = recheckSchedulerService;
        _inspectionScopeService = inspectionScopeService;
        _deviceCatalogService = deviceCatalogService;
        _deviceInspectionService = deviceInspectionService;
        _mediaReview = new DeviceMediaReviewViewModel(playbackReviewService, screenshotSamplingService, cloudPlaybackService);
        _faultClosureService.OverviewChanged += OnFaultClosureOverviewChanged;
        _recheckSchedulerService.OverviewChanged += OnRecheckOverviewChanged;

        StatusBadgeText = pageData.StatusBadgeText;
        StatusBadgeAccentResourceKey = pageData.StatusBadgeAccentResourceKey;

        SummaryCards = new ObservableCollection<OverviewMetric>();
        StatusOptions = new ObservableCollection<SelectionItemViewModel>(BuildStatusOptions());
        SourceOptions = new ObservableCollection<SelectionItemViewModel>(BuildSourceOptions());
        FaultTypeOptions = new ObservableCollection<SelectionItemViewModel>(BuildAllOption("全部故障类型"));
        Records = new ObservableCollection<FaultClosureRecord>();
        RecheckTasks = new ObservableCollection<RecheckTaskRecord>();
        SelectedTaskExecutions = new ObservableCollection<RecheckExecutionRecord>();
        FaultTypeRuleOptions = new ObservableCollection<SelectionItemViewModel>(BuildFaultTypeRuleOptions(Array.Empty<string>()));
        ConfiguredFaultTypeRules = new ObservableCollection<RecheckScheduleRule>();

        _selectedStatusOption = StatusOptions.FirstOrDefault();
        _selectedSourceOption = SourceOptions.FirstOrDefault();
        _selectedFaultTypeOption = FaultTypeOptions.FirstOrDefault();
        _selectedFaultTypeRuleOption = FaultTypeRuleOptions.FirstOrDefault();

        RefreshCommand = new RelayCommand<object?>(_ => _ = RefreshAsync(preserveSelection: true));
        RefreshRecheckQueueCommand = new RelayCommand<object?>(_ => RefreshRecheckQueue());
        SaveRecheckRuleCommand = new RelayCommand<object?>(_ => _ = SaveRecheckRuleAsync());
        RestoreDefaultRecheckRuleCommand = new RelayCommand<object?>(_ => _ = RestoreDefaultRecheckRuleAsync());
        SaveFaultTypeRuleCommand = new RelayCommand<object?>(_ => _ = SaveFaultTypeRuleAsync());
        RemoveFaultTypeRuleCommand = new RelayCommand<object?>(_ => _ = RemoveFaultTypeRuleAsync());
        AddToRecheckQueueCommand = new RelayCommand<object?>(_ => _ = AddToRecheckQueueAsync());
        TriggerSelectedTaskCommand = new RelayCommand<object?>(_ => _ = TriggerSelectedTaskAsync());
        ToggleSelectedTaskEnabledCommand = new RelayCommand<object?>(_ => _ = ToggleSelectedTaskEnabledAsync());
        SaveSelectedTaskOverrideCommand = new RelayCommand<object?>(_ => _ = SaveSelectedTaskOverrideAsync());
        ClearSelectedTaskOverrideCommand = new RelayCommand<object?>(_ => _ = ClearSelectedTaskOverrideAsync());
        MarkDispatchedCommand = new RelayCommand<object?>(_ => _ = MarkDispatchedAsync());
        RunRecheckCommand = new RelayCommand<object?>(_ => _ = RunRecheckAsync());
        ClearRecoveredCommand = new RelayCommand<object?>(_ => _ = ClearRecoveredAsync());
        CloseRecordCommand = new RelayCommand<object?>(_ => _ = CloseRecordAsync());
        CloseFalsePositiveCommand = new RelayCommand<object?>(_ => _ = CloseAsFalsePositiveAsync());
        ResetActionDraftCommand = new RelayCommand<object?>(_ => ResetActionDraft());

        _ = RefreshAsync(preserveSelection: false);
    }

    public string StatusBadgeText { get; }

    public string StatusBadgeAccentResourceKey { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<SelectionItemViewModel> StatusOptions { get; }

    public ObservableCollection<SelectionItemViewModel> SourceOptions { get; }

    public ObservableCollection<SelectionItemViewModel> FaultTypeOptions { get; }

    public ObservableCollection<SelectionItemViewModel> FaultTypeRuleOptions { get; }

    public ObservableCollection<FaultClosureRecord> Records { get; }

    public ObservableCollection<RecheckTaskRecord> RecheckTasks { get; }

    public ObservableCollection<RecheckScheduleRule> ConfiguredFaultTypeRules { get; }

    public ObservableCollection<RecheckExecutionRecord> SelectedTaskExecutions { get; }

    public DeviceMediaReviewViewModel MediaReview => _mediaReview;

    public SelectionItemViewModel? SelectedStatusOption
    {
        get => _selectedStatusOption;
        set
        {
            if (!SetProperty(ref _selectedStatusOption, value))
            {
                return;
            }

            TriggerFilterRefresh();
        }
    }

    public SelectionItemViewModel? SelectedSourceOption
    {
        get => _selectedSourceOption;
        set
        {
            if (!SetProperty(ref _selectedSourceOption, value))
            {
                return;
            }

            TriggerFilterRefresh();
        }
    }

    public SelectionItemViewModel? SelectedFaultTypeOption
    {
        get => _selectedFaultTypeOption;
        set
        {
            if (!SetProperty(ref _selectedFaultTypeOption, value))
            {
                return;
            }

            TriggerFilterRefresh();
        }
    }

    public SelectionItemViewModel? SelectedFaultTypeRuleOption
    {
        get => _selectedFaultTypeRuleOption;
        set
        {
            if (!SetProperty(ref _selectedFaultTypeRuleOption, value))
            {
                return;
            }

            LoadSelectedFaultTypeRuleIntoEditor();
        }
    }

    public FaultClosureRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (!SetProperty(ref _selectedRecord, value))
            {
                return;
            }

            SyncMediaReviewContext();
            SyncSelectedTaskFromRecord();
            RaiseSelectedRecordChanged();
        }
    }

    public RecheckTaskRecord? SelectedRecheckTask
    {
        get => _selectedRecheckTask;
        set
        {
            if (!SetProperty(ref _selectedRecheckTask, value))
            {
                return;
            }

            if (value is not null)
            {
                SelectedRecord = Records.FirstOrDefault(item =>
                    string.Equals(item.RecordId, value.SourceFaultClosureId, StringComparison.OrdinalIgnoreCase))
                    ?? SelectedRecord;
            }

            SyncSelectedTaskExecutions();
            SyncSelectedTaskOverrideEditor();
            RaiseSelectedTaskChanged();
        }
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

    public string RecheckQueueStatusText
    {
        get => _recheckQueueStatusText;
        private set => SetProperty(ref _recheckQueueStatusText, value);
    }

    public string RecheckRuleInitialDelayMinutesText
    {
        get => _recheckRuleInitialDelayMinutesText;
        set
        {
            if (SetProperty(ref _recheckRuleInitialDelayMinutesText, value))
            {
                RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
            }
        }
    }

    public string RecheckRuleIntervalMinutesText
    {
        get => _recheckRuleIntervalMinutesText;
        set
        {
            if (SetProperty(ref _recheckRuleIntervalMinutesText, value))
            {
                RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
            }
        }
    }

    public string RecheckRuleRetryDelayMinutesText
    {
        get => _recheckRuleRetryDelayMinutesText;
        set
        {
            if (SetProperty(ref _recheckRuleRetryDelayMinutesText, value))
            {
                RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
            }
        }
    }

    public string RecheckRuleMaxRetryCountText
    {
        get => _recheckRuleMaxRetryCountText;
        set
        {
            if (SetProperty(ref _recheckRuleMaxRetryCountText, value))
            {
                RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
            }
        }
    }

    public bool RecheckRuleAutoEnabled
    {
        get => _recheckRuleAutoEnabled;
        set
        {
            if (SetProperty(ref _recheckRuleAutoEnabled, value))
            {
                RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
            }
        }
    }

    public bool RecheckRuleAllowManualOverride
    {
        get => _recheckRuleAllowManualOverride;
        set
        {
            if (SetProperty(ref _recheckRuleAllowManualOverride, value))
            {
                RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
            }
        }
    }

    public string RecheckRuleFeedbackText
    {
        get => _recheckRuleFeedbackText;
        private set => SetProperty(ref _recheckRuleFeedbackText, value);
    }

    public string FaultTypeRuleInitialDelayMinutesText
    {
        get => _faultTypeRuleInitialDelayMinutesText;
        set => SetProperty(ref _faultTypeRuleInitialDelayMinutesText, value);
    }

    public string FaultTypeRuleIntervalMinutesText
    {
        get => _faultTypeRuleIntervalMinutesText;
        set => SetProperty(ref _faultTypeRuleIntervalMinutesText, value);
    }

    public string FaultTypeRuleRetryDelayMinutesText
    {
        get => _faultTypeRuleRetryDelayMinutesText;
        set => SetProperty(ref _faultTypeRuleRetryDelayMinutesText, value);
    }

    public string FaultTypeRuleMaxRetryCountText
    {
        get => _faultTypeRuleMaxRetryCountText;
        set => SetProperty(ref _faultTypeRuleMaxRetryCountText, value);
    }

    public bool FaultTypeRuleAutoEnabled
    {
        get => _faultTypeRuleAutoEnabled;
        set => SetProperty(ref _faultTypeRuleAutoEnabled, value);
    }

    public string FaultTypeRuleFeedbackText
    {
        get => _faultTypeRuleFeedbackText;
        private set => SetProperty(ref _faultTypeRuleFeedbackText, value);
    }

    public string SelectedTaskOverrideInitialDelayMinutesText
    {
        get => _selectedTaskOverrideInitialDelayMinutesText;
        set => SetProperty(ref _selectedTaskOverrideInitialDelayMinutesText, value);
    }

    public string SelectedTaskOverrideIntervalMinutesText
    {
        get => _selectedTaskOverrideIntervalMinutesText;
        set => SetProperty(ref _selectedTaskOverrideIntervalMinutesText, value);
    }

    public string SelectedTaskOverrideRetryDelayMinutesText
    {
        get => _selectedTaskOverrideRetryDelayMinutesText;
        set => SetProperty(ref _selectedTaskOverrideRetryDelayMinutesText, value);
    }

    public string SelectedTaskOverrideMaxRetryCountText
    {
        get => _selectedTaskOverrideMaxRetryCountText;
        set => SetProperty(ref _selectedTaskOverrideMaxRetryCountText, value);
    }

    public bool SelectedTaskOverrideAutoEnabled
    {
        get => _selectedTaskOverrideAutoEnabled;
        set => SetProperty(ref _selectedTaskOverrideAutoEnabled, value);
    }

    public bool SelectedTaskOverrideEnabled
    {
        get => _selectedTaskOverrideEnabled;
        set => SetProperty(ref _selectedTaskOverrideEnabled, value);
    }

    public string SelectedTaskOverrideFeedbackText
    {
        get => _selectedTaskOverrideFeedbackText;
        private set => SetProperty(ref _selectedTaskOverrideFeedbackText, value);
    }

    public string OperatorName
    {
        get => _operatorName;
        set => SetProperty(ref _operatorName, value);
    }

    public string ActionNote
    {
        get => _actionNote;
        set => SetProperty(ref _actionNote, value);
    }

    public bool PendingRecheckOnly
    {
        get => _pendingRecheckOnly;
        set
        {
            if (!SetProperty(ref _pendingRecheckOnly, value))
            {
                return;
            }

            TriggerFilterRefresh();
        }
    }

    public bool FocusedOnly
    {
        get => _focusedOnly;
        set
        {
            if (!SetProperty(ref _focusedOnly, value))
            {
                return;
            }

            TriggerFilterRefresh();
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

    public bool IsProcessingAction
    {
        get => _isProcessingAction;
        private set
        {
            if (SetProperty(ref _isProcessingAction, value))
            {
                RaisePropertyChanged(nameof(CanMarkDispatched));
                RaisePropertyChanged(nameof(CanRunRecheck));
                RaisePropertyChanged(nameof(CanAddToRecheckQueue));
                RaisePropertyChanged(nameof(CanTriggerSelectedTask));
                RaisePropertyChanged(nameof(CanToggleSelectedTaskEnabled));
                RaisePropertyChanged(nameof(CanSaveRecheckRule));
                RaisePropertyChanged(nameof(CanRestoreDefaultRecheckRule));
                RaisePropertyChanged(nameof(CanSaveFaultTypeRule));
                RaisePropertyChanged(nameof(CanRemoveFaultTypeRule));
                RaisePropertyChanged(nameof(CanSaveSelectedTaskOverride));
                RaisePropertyChanged(nameof(CanClearSelectedTaskOverride));
                RaisePropertyChanged(nameof(CanClearRecovered));
                RaisePropertyChanged(nameof(CanCloseRecord));
                RaisePropertyChanged(nameof(CanCloseFalsePositive));
            }
        }
    }

    public bool CanRefresh => !IsRefreshing && !IsProcessingAction;

    public bool HasSelectedRecord => SelectedRecord is not null;

    public bool HasSelectedImage => SelectedRecord?.HasEvidenceImage == true;

    public bool CanMarkDispatched => !IsProcessingAction && SelectedRecord?.CanMarkDispatched == true;

    public bool CanRunRecheck => !IsProcessingAction && SelectedRecord?.CanRunRecheck == true;

    public bool CanAddToRecheckQueue => !IsProcessingAction &&
        SelectedRecord is not null &&
        SelectedRecord.RequiresRecheck &&
        string.Equals(SelectedRecord.CurrentStatus, FaultClosureStatuses.PendingRecheck, StringComparison.OrdinalIgnoreCase);

    public bool HasSelectedTask => SelectedRecheckTask is not null;

    public bool CanTriggerSelectedTask => !IsProcessingAction &&
        SelectedRecheckTask is not null &&
        !SelectedRecheckTask.IsTerminalTask &&
        !SelectedRecheckTask.IsRunning;

    public bool CanToggleSelectedTaskEnabled => !IsProcessingAction &&
        SelectedRecheckTask is not null &&
        !SelectedRecheckTask.IsTerminalTask;

    public bool CanSaveRecheckRule => !IsProcessingAction;

    public bool CanRestoreDefaultRecheckRule => !IsProcessingAction;

    public bool CanSaveFaultTypeRule => !IsProcessingAction && SelectedFaultTypeRuleOption is not null;

    public bool CanRemoveFaultTypeRule => !IsProcessingAction &&
        SelectedFaultTypeRuleOption is not null &&
        ConfiguredFaultTypeRules.Any(item => string.Equals(item.ScopeKey, SelectedFaultTypeRuleOption.Key, StringComparison.OrdinalIgnoreCase));

    public bool CanClearRecovered => !IsProcessingAction && SelectedRecord?.CanClear == true;

    public bool CanCloseRecord => !IsProcessingAction && SelectedRecord?.CanClose == true;

    public bool CanCloseFalsePositive => !IsProcessingAction && SelectedRecord?.CanClose == true;

    public string SelectedImageUri => SelectedRecord?.PrimaryEvidenceImagePath ?? string.Empty;

    public string SelectedTicketNumber => SelectedRecord?.TicketNumber ?? "--";

    public string SelectedStatusText => SelectedRecord?.StatusText ?? "--";

    public string SelectedSourceText => SelectedRecord?.SourceTypeText ?? "--";

    public string SelectedFaultTypeText => SelectedRecord?.FaultType ?? "--";

    public string SelectedReviewConclusionText => SelectedRecord?.ReviewConclusionText ?? "--";

    public string SelectedSummaryText => SelectedRecord?.FaultSummary ?? "选择闭环记录后查看故障摘要。";

    public string SelectedPointNameText => SelectedRecord?.DeviceName ?? "未选择点位";

    public string SelectedPointCodeText => SelectedRecord?.DeviceCode ?? "--";

    public string SelectedDirectoryText => SelectedRecord?.DirectoryPath ?? "--";

    public string SelectedEvidenceSummaryText => SelectedRecord?.EvidenceSummaryText ?? "--";

    public string SelectedAiAlertText => FirstNonEmpty(SelectedRecord?.RelatedAiAlertId, SelectedRecord?.AiAlertSummary, "--");

    public string SelectedPlaybackText => FirstNonEmpty(SelectedRecord?.PlaybackFileName, SelectedRecord?.RelatedPlaybackReviewSessionId, "--");

    public string SelectedPrimaryEvidenceTimeText => SelectedRecord?.PrimaryEvidenceCapturedAtText ?? "--";

    public string SelectedActionNoteText => SelectedRecord?.LastActionNote ?? "--";

    public string SelectedDispatchChannelsText => SelectedRecord?.DispatchDraft.ReservedChannelsText ?? "--";

    public string SelectedDispatchCreatedText => SelectedRecord?.DispatchDraft.CreatedAtText ?? "--";

    public string SelectedDispatchStatusText => SelectedRecord?.DispatchDraft.CurrentStatusText ?? "--";

    public string SelectedDispatchDispatchedText => SelectedRecord?.DispatchDraft.DispatchedAtText ?? "--";

    public string SelectedLatestRecheckText => SelectedRecord?.LatestRecheckText ?? "未复检";

    public string SelectedTaskStatusText => SelectedRecheckTask?.CurrentStatusText ?? "--";

    public string SelectedTaskNextRunText => SelectedRecheckTask?.NextRunAtText ?? "--";

    public string SelectedTaskRuleText => SelectedRecheckTask?.ScheduleRule.RuleSummaryText ?? "--";

    public string SelectedTaskLatestResultText => SelectedRecheckTask?.LastExecutionSummaryText ?? "暂无执行结果";

    public string SelectedTaskEnabledText => SelectedRecheckTask?.EnabledStatusText ?? "--";

    public string SelectedTaskRuleSourceText => SelectedRecheckTask?.RuleOriginSummaryText ?? "--";

    public bool CanSaveSelectedTaskOverride => !IsProcessingAction &&
        SelectedRecheckTask is not null &&
        !SelectedRecheckTask.IsTerminalTask &&
        _currentRuleCatalog.GlobalDefaultRule.AllowManualOverride;

    public bool CanClearSelectedTaskOverride => !IsProcessingAction &&
        SelectedRecheckTask?.HasManualRuleOverride == true;

    public string CurrentRecheckRuleSummaryText => _currentRecheckRule.RuleSummaryText;

    public string CurrentRecheckRuleAutoText => _currentRecheckRule.AutoExecutionText;

    public string CurrentRecheckRuleManualOverrideText => _currentRecheckRule.ManualOverrideText;

    public string SelectedFaultTypeRuleSummaryText => ResolveSelectedFaultTypeRule()?.RuleSummaryText ?? _currentRuleCatalog.GlobalDefaultRule.RuleSummaryText;

    public string SelectedFaultTypeRuleStateText => ResolveSelectedFaultTypeRule() is null
        ? "当前故障类型未单独配置，默认继承全局规则"
        : "当前故障类型已配置差异化规则";

    public string CurrentRecheckRuleDraftStateText => HasUnsavedRecheckRuleDraft()
        ? "规则草稿尚未保存"
        : "编辑区与当前生效规则一致";

    public string ToggleTaskEnabledButtonText => SelectedRecheckTask?.IsEnabled == true ? "停用任务" : "启用任务";

    public string SelectedClearTrailText => SelectedRecord is not null && SelectedRecord.ClearRecords.Count > 0
        ? string.Join(" / ", SelectedRecord.ClearRecords
            .OrderByDescending(item => item.PerformedAt)
            .Take(2)
            .Select(item => $"{item.ActionTypeText} {item.PerformedAt:MM-dd HH:mm}"))
        : "未发生销警/关闭动作";

    public ICommand RefreshCommand { get; }

    public ICommand RefreshRecheckQueueCommand { get; }

    public ICommand SaveRecheckRuleCommand { get; }

    public ICommand RestoreDefaultRecheckRuleCommand { get; }

    public ICommand SaveFaultTypeRuleCommand { get; }

    public ICommand RemoveFaultTypeRuleCommand { get; }

    public ICommand AddToRecheckQueueCommand { get; }

    public ICommand TriggerSelectedTaskCommand { get; }

    public ICommand ToggleSelectedTaskEnabledCommand { get; }

    public ICommand SaveSelectedTaskOverrideCommand { get; }

    public ICommand ClearSelectedTaskOverrideCommand { get; }

    public ICommand MarkDispatchedCommand { get; }

    public ICommand RunRecheckCommand { get; }

    public ICommand ClearRecoveredCommand { get; }

    public ICommand CloseRecordCommand { get; }

    public ICommand CloseFalsePositiveCommand { get; }

    public ICommand ResetActionDraftCommand { get; }

    private async Task RefreshAsync(bool preserveSelection)
    {
        if (IsRefreshing)
        {
            return;
        }

        var preferredRecordId = preserveSelection ? SelectedRecord?.RecordId : null;
        IsRefreshing = true;
        WarningText = string.Empty;
        StatusText = "正在刷新故障闭环中心...";

        try
        {
            var overview = await Task.Run(() => _faultClosureService.GetOverview(new FaultClosureQuery
            {
                Status = SelectedStatusOption?.Key ?? string.Empty,
                FaultType = SelectedFaultTypeOption?.Key ?? string.Empty,
                SourceType = SelectedSourceOption?.Key ?? string.Empty,
                PendingRecheckOnly = PendingRecheckOnly,
                FocusedOnly = FocusedOnly
            }));

            ReplaceCollection(Records, overview.Records);
            UpdateFaultTypeOptions(overview.FaultTypes);
            UpdateSummaryCards(overview);
            LastUpdatedText = overview.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            StatusText = overview.StatusMessage;
            WarningText = overview.WarningMessage;

            SelectedRecord = !string.IsNullOrWhiteSpace(preferredRecordId)
                ? Records.FirstOrDefault(item => string.Equals(item.RecordId, preferredRecordId, StringComparison.OrdinalIgnoreCase))
                : Records.FirstOrDefault();
            RefreshRecheckQueue(SelectedRecheckTask?.TaskId);
        }
        catch (Exception ex)
        {
            StatusText = "故障闭环中心刷新失败。";
            WarningText = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task MarkDispatchedAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await ExecuteActionAsync(
            () => _faultClosureService.MarkDispatched(SelectedRecord.RecordId, OperatorName, ActionNote),
            "正在标记已派单...");
    }

    private void OnFaultClosureOverviewChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => _ = RefreshAsync(preserveSelection: true)));
    }

    private void OnRecheckOverviewChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() => RefreshRecheckQueue()));
    }

    private async Task AddToRecheckQueueAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await ExecuteTaskActionAsync(
            () => _recheckSchedulerService.EnsureTask(SelectedRecord, OperatorName),
            "正在加入本地复检队列...");
    }

    private async Task TriggerSelectedTaskAsync()
    {
        if (SelectedRecheckTask is null)
        {
            return;
        }

        await ExecuteTaskActionAsync(
            () => _recheckSchedulerService.TriggerTaskNow(SelectedRecheckTask.TaskId, OperatorName),
            "正在手动执行复检任务...");
    }

    private async Task ToggleSelectedTaskEnabledAsync()
    {
        if (SelectedRecheckTask is null)
        {
            return;
        }

        await ExecuteTaskActionAsync(
            () => _recheckSchedulerService.SetTaskEnabled(
                SelectedRecheckTask.TaskId,
                !SelectedRecheckTask.IsEnabled,
                OperatorName),
            SelectedRecheckTask.IsEnabled ? "正在停用复检任务..." : "正在启用复检任务...");
    }

    private async Task RunRecheckAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await ExecuteActionAsync(
            () => _faultClosureService.RunRecheck(SelectedRecord.RecordId, OperatorName),
            "正在执行单点复检...");
    }

    private async Task ClearRecoveredAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await ExecuteActionAsync(
            () => _faultClosureService.ClearRecovered(SelectedRecord.RecordId, OperatorName, ActionNote),
            "正在执行本地销警...");
    }

    private async Task CloseRecordAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await ExecuteActionAsync(
            () => _faultClosureService.CloseRecord(SelectedRecord.RecordId, OperatorName, ActionNote),
            "正在关闭闭环记录...");
    }

    private async Task CloseAsFalsePositiveAsync()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        await ExecuteActionAsync(
            () => _faultClosureService.CloseAsFalsePositive(SelectedRecord.RecordId, OperatorName, ActionNote),
            "正在按误报关闭...");
    }

    private async Task ExecuteActionAsync(Func<FaultClosureRecord> action, string busyText)
    {
        if (IsProcessingAction)
        {
            return;
        }

        var preserveRecordId = SelectedRecord?.RecordId;
        IsProcessingAction = true;
        WarningText = string.Empty;
        StatusText = busyText;

        try
        {
            await Task.Run(action);
            await RefreshAsync(preserveSelection: true);
            if (!string.IsNullOrWhiteSpace(preserveRecordId))
            {
                SelectedRecord = Records.FirstOrDefault(item => string.Equals(item.RecordId, preserveRecordId, StringComparison.OrdinalIgnoreCase)) ?? SelectedRecord;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{busyText}失败。";
            WarningText = ex.Message;
        }
        finally
        {
            IsProcessingAction = false;
        }
    }

    private async Task ExecuteTaskActionAsync(Func<RecheckTaskRecord> action, string busyText)
    {
        if (IsProcessingAction)
        {
            return;
        }

        var preferredTaskId = SelectedRecheckTask?.TaskId;
        IsProcessingAction = true;
        WarningText = string.Empty;
        RecheckQueueStatusText = busyText;

        try
        {
            await Task.Run(action);
            RefreshRecheckQueue(preferredTaskId);
            await RefreshAsync(preserveSelection: true);
        }
        catch (Exception ex)
        {
            WarningText = ex.Message;
            RecheckQueueStatusText = $"{busyText}失败。";
        }
        finally
        {
            IsProcessingAction = false;
        }
    }

    private async Task SaveRecheckRuleAsync()
    {
        if (!TryBuildRecheckRuleFromDraft(out var rule, out var errorMessage))
        {
            RecheckRuleFeedbackText = errorMessage;
            return;
        }

        await ExecuteRuleActionAsync(
            () => _recheckSchedulerService.SaveScheduleRule(rule, OperatorName),
            "正在保存复检规则...",
            "复检规则已保存。");
    }

    private async Task RestoreDefaultRecheckRuleAsync()
    {
        await ExecuteRuleActionAsync(
            () => _recheckSchedulerService.RestoreDefaultScheduleRule(OperatorName),
            "正在恢复默认复检规则...",
            "已恢复默认复检规则。");
    }

    private async Task ExecuteRuleActionAsync(
        Func<RecheckScheduleRule> action,
        string busyText,
        string successText)
    {
        if (IsProcessingAction)
        {
            return;
        }

        IsProcessingAction = true;
        WarningText = string.Empty;
        RecheckRuleFeedbackText = busyText;

        try
        {
            var savedRule = await Task.Run(action);
            LoadRecheckRule(savedRule, forceEditorSync: true);
            RecheckRuleFeedbackText = successText;
            RefreshRecheckQueue(SelectedRecheckTask?.TaskId);
        }
        catch (Exception ex)
        {
            WarningText = ex.Message;
            RecheckRuleFeedbackText = $"{busyText}失败。";
        }
        finally
        {
            IsProcessingAction = false;
        }
    }

    private void RefreshRecheckQueue(string? preferredTaskId = null)
    {
        try
        {
            var overview = _recheckSchedulerService.GetOverview();
            ReplaceCollection(RecheckTasks, overview.Tasks);
            RecheckQueueStatusText = overview.StatusMessage;
            LoadRuleCatalog(overview.RuleCatalog, forceEditorSync: false);

            SelectedRecheckTask = !string.IsNullOrWhiteSpace(preferredTaskId)
                ? RecheckTasks.FirstOrDefault(item => string.Equals(item.TaskId, preferredTaskId, StringComparison.OrdinalIgnoreCase))
                : ResolvePreferredTask();
        }
        catch (Exception ex)
        {
            RecheckQueueStatusText = ex.Message;
        }
    }

    private void LoadRuleCatalog(RecheckRuleCatalog ruleCatalog, bool forceEditorSync)
    {
        _currentRuleCatalog = ruleCatalog.Normalize();
        LoadRecheckRule(_currentRuleCatalog.GlobalDefaultRule, forceEditorSync);
        ReplaceCollection(ConfiguredFaultTypeRules, _currentRuleCatalog.FaultTypeRules);
        UpdateFaultTypeRuleOptions();
        LoadSelectedFaultTypeRuleIntoEditor();
        SyncSelectedTaskOverrideEditor();
        RaisePropertyChanged(nameof(CanSaveSelectedTaskOverride));
        RaisePropertyChanged(nameof(CanClearSelectedTaskOverride));
    }

    private void LoadRecheckRule(RecheckScheduleRule rule, bool forceEditorSync)
    {
        _currentRecheckRule = rule.Normalize();
        RaisePropertyChanged(nameof(CurrentRecheckRuleSummaryText));
        RaisePropertyChanged(nameof(CurrentRecheckRuleAutoText));
        RaisePropertyChanged(nameof(CurrentRecheckRuleManualOverrideText));
        RaisePropertyChanged(nameof(SelectedFaultTypeRuleSummaryText));
        RaisePropertyChanged(nameof(SelectedFaultTypeRuleStateText));

        if (forceEditorSync || !_hasInitializedRuleEditor || !HasUnsavedRecheckRuleDraft())
        {
            ApplyRecheckRuleToEditor(_currentRecheckRule);
        }

        RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
    }

    private void ApplyRecheckRuleToEditor(RecheckScheduleRule rule)
    {
        _hasInitializedRuleEditor = true;
        RecheckRuleInitialDelayMinutesText = rule.InitialDelayMinutes.ToString();
        RecheckRuleIntervalMinutesText = rule.IntervalMinutes.ToString();
        RecheckRuleRetryDelayMinutesText = rule.RetryDelayMinutes.ToString();
        RecheckRuleMaxRetryCountText = rule.MaxRetryCount.ToString();
        RecheckRuleAutoEnabled = rule.IsAutoRecheckEnabled;
        RecheckRuleAllowManualOverride = rule.AllowManualOverride;
        RaisePropertyChanged(nameof(CurrentRecheckRuleDraftStateText));
    }

    private bool HasUnsavedRecheckRuleDraft()
    {
        if (!_hasInitializedRuleEditor)
        {
            return false;
        }

        return !TryBuildRecheckRuleFromDraft(out var draftRule, out _)
            || draftRule.InitialDelayMinutes != _currentRecheckRule.InitialDelayMinutes
            || draftRule.IntervalMinutes != _currentRecheckRule.IntervalMinutes
            || draftRule.RetryDelayMinutes != _currentRecheckRule.RetryDelayMinutes
            || draftRule.MaxRetryCount != _currentRecheckRule.MaxRetryCount
            || draftRule.IsAutoRecheckEnabled != _currentRecheckRule.IsAutoRecheckEnabled
            || draftRule.AllowManualOverride != _currentRecheckRule.AllowManualOverride;
    }

    private bool TryBuildRecheckRuleFromDraft(out RecheckScheduleRule rule, out string errorMessage)
    {
        errorMessage = string.Empty;
        rule = _currentRecheckRule;

        if (!TryParseNonNegativeInt(RecheckRuleInitialDelayMinutesText, "首次延迟分钟数", out var initialDelay, out errorMessage) ||
            !TryParsePositiveInt(RecheckRuleIntervalMinutesText, "复检间隔分钟数", out var intervalMinutes, out errorMessage) ||
            !TryParsePositiveInt(RecheckRuleRetryDelayMinutesText, "失败重试间隔分钟数", out var retryDelayMinutes, out errorMessage) ||
            !TryParseNonNegativeInt(RecheckRuleMaxRetryCountText, "最大重试次数", out var maxRetryCount, out errorMessage))
        {
            return false;
        }

        rule = _currentRecheckRule with
        {
            InitialDelayMinutes = initialDelay,
            IntervalMinutes = intervalMinutes,
            RetryDelayMinutes = retryDelayMinutes,
            MaxRetryCount = maxRetryCount,
            IsAutoRecheckEnabled = RecheckRuleAutoEnabled,
            AllowManualOverride = RecheckRuleAllowManualOverride
        };
        return true;
    }

    private async Task SaveFaultTypeRuleAsync()
    {
        if (SelectedFaultTypeRuleOption is null)
        {
            return;
        }

        var faultTypeKey = SelectedFaultTypeRuleOption.Key;
        if (string.IsNullOrWhiteSpace(faultTypeKey))
        {
            FaultTypeRuleFeedbackText = "请选择需要配置的故障类型。";
            return;
        }

        if (!TryBuildFaultTypeRuleFromDraft(out var rule, out var errorMessage))
        {
            FaultTypeRuleFeedbackText = errorMessage;
            return;
        }

        await ExecuteFaultTypeRuleActionAsync(
            () => _recheckSchedulerService.SaveFaultTypeRule(faultTypeKey, rule, OperatorName),
            "正在保存故障类型规则...",
            "故障类型规则已保存。");
    }

    private async Task RemoveFaultTypeRuleAsync()
    {
        if (SelectedFaultTypeRuleOption is null)
        {
            return;
        }

        var faultTypeKey = SelectedFaultTypeRuleOption.Key;
        if (string.IsNullOrWhiteSpace(faultTypeKey))
        {
            FaultTypeRuleFeedbackText = "请选择需要移除规则的故障类型。";
            return;
        }

        if (IsProcessingAction)
        {
            return;
        }

        IsProcessingAction = true;
        WarningText = string.Empty;
        FaultTypeRuleFeedbackText = "正在移除故障类型规则...";

        try
        {
            await Task.Run(() => _recheckSchedulerService.RemoveFaultTypeRule(faultTypeKey, OperatorName));
            RefreshRecheckQueue(SelectedRecheckTask?.TaskId);
            FaultTypeRuleFeedbackText = "故障类型规则已移除。";
        }
        catch (Exception ex)
        {
            WarningText = ex.Message;
            FaultTypeRuleFeedbackText = "移除故障类型规则失败。";
        }
        finally
        {
            IsProcessingAction = false;
        }
    }

    private async Task ExecuteFaultTypeRuleActionAsync(
        Func<RecheckScheduleRule> action,
        string busyText,
        string successText)
    {
        if (IsProcessingAction)
        {
            return;
        }

        IsProcessingAction = true;
        WarningText = string.Empty;
        FaultTypeRuleFeedbackText = busyText;

        try
        {
            await Task.Run(action);
            RefreshRecheckQueue(SelectedRecheckTask?.TaskId);
            FaultTypeRuleFeedbackText = successText;
        }
        catch (Exception ex)
        {
            WarningText = ex.Message;
            FaultTypeRuleFeedbackText = $"{busyText}失败。";
        }
        finally
        {
            IsProcessingAction = false;
        }
    }

    private async Task SaveSelectedTaskOverrideAsync()
    {
        if (SelectedRecheckTask is null)
        {
            return;
        }

        if (!SelectedTaskOverrideEnabled)
        {
            await ClearSelectedTaskOverrideAsync();
            return;
        }

        if (!TryBuildSelectedTaskOverrideRule(out var rule, out var errorMessage))
        {
            SelectedTaskOverrideFeedbackText = errorMessage;
            return;
        }

        await ExecuteTaskActionAsync(
            () => _recheckSchedulerService.SaveTaskRuleOverride(SelectedRecheckTask.TaskId, rule, OperatorName),
            "正在保存单任务规则覆盖...");
        SelectedTaskOverrideFeedbackText = "单任务规则覆盖已保存。";
    }

    private async Task ClearSelectedTaskOverrideAsync()
    {
        if (SelectedRecheckTask is null)
        {
            return;
        }

        await ExecuteTaskActionAsync(
            () => _recheckSchedulerService.ClearTaskRuleOverride(SelectedRecheckTask.TaskId, OperatorName),
            "正在清除单任务规则覆盖...");
        SelectedTaskOverrideFeedbackText = "当前任务已恢复继承规则。";
    }

    private bool TryBuildFaultTypeRuleFromDraft(out RecheckScheduleRule rule, out string errorMessage)
    {
        errorMessage = string.Empty;
        rule = ResolveSelectedFaultTypeRule() ?? _currentRuleCatalog.GlobalDefaultRule;

        if (!TryParseNonNegativeInt(FaultTypeRuleInitialDelayMinutesText, "故障类型首次延迟", out var initialDelay, out errorMessage) ||
            !TryParsePositiveInt(FaultTypeRuleIntervalMinutesText, "故障类型复检间隔", out var intervalMinutes, out errorMessage) ||
            !TryParsePositiveInt(FaultTypeRuleRetryDelayMinutesText, "故障类型失败重试", out var retryDelayMinutes, out errorMessage) ||
            !TryParseNonNegativeInt(FaultTypeRuleMaxRetryCountText, "故障类型最大重试次数", out var maxRetryCount, out errorMessage))
        {
            return false;
        }

        rule = rule with
        {
            InitialDelayMinutes = initialDelay,
            IntervalMinutes = intervalMinutes,
            RetryDelayMinutes = retryDelayMinutes,
            MaxRetryCount = maxRetryCount,
            IsAutoRecheckEnabled = FaultTypeRuleAutoEnabled,
            AllowManualOverride = _currentRuleCatalog.GlobalDefaultRule.AllowManualOverride,
            ScopeType = RecheckRuleScopeTypes.FaultType,
            ScopeKey = SelectedFaultTypeRuleOption?.Key ?? string.Empty
        };
        return true;
    }

    private bool TryBuildSelectedTaskOverrideRule(out RecheckScheduleRule rule, out string errorMessage)
    {
        errorMessage = string.Empty;
        rule = SelectedRecheckTask?.ScheduleRule ?? _currentRuleCatalog.GlobalDefaultRule;

        if (!TryParseNonNegativeInt(SelectedTaskOverrideInitialDelayMinutesText, "任务首次延迟", out var initialDelay, out errorMessage) ||
            !TryParsePositiveInt(SelectedTaskOverrideIntervalMinutesText, "任务复检间隔", out var intervalMinutes, out errorMessage) ||
            !TryParsePositiveInt(SelectedTaskOverrideRetryDelayMinutesText, "任务失败重试", out var retryDelayMinutes, out errorMessage) ||
            !TryParseNonNegativeInt(SelectedTaskOverrideMaxRetryCountText, "任务最大重试次数", out var maxRetryCount, out errorMessage))
        {
            return false;
        }

        rule = rule with
        {
            InitialDelayMinutes = initialDelay,
            IntervalMinutes = intervalMinutes,
            RetryDelayMinutes = retryDelayMinutes,
            MaxRetryCount = maxRetryCount,
            IsAutoRecheckEnabled = SelectedTaskOverrideAutoEnabled,
            AllowManualOverride = true,
            ScopeType = RecheckRuleScopeTypes.ManualOverride,
            ScopeKey = SelectedRecheckTask?.TaskId ?? string.Empty
        };
        return true;
    }

    private void UpdateFaultTypeRuleOptions()
    {
        var selectedKey = SelectedFaultTypeRuleOption?.Key ?? string.Empty;
        ReplaceCollection(FaultTypeRuleOptions, BuildFaultTypeRuleOptions(FaultTypeOptions
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .Select(item => item.Key!)));
        SelectedFaultTypeRuleOption = FaultTypeRuleOptions.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
            ?? FaultTypeRuleOptions.FirstOrDefault();
    }

    private void LoadSelectedFaultTypeRuleIntoEditor()
    {
        var rule = ResolveSelectedFaultTypeRule() ?? _currentRuleCatalog.GlobalDefaultRule;
        FaultTypeRuleInitialDelayMinutesText = rule.InitialDelayMinutes.ToString();
        FaultTypeRuleIntervalMinutesText = rule.IntervalMinutes.ToString();
        FaultTypeRuleRetryDelayMinutesText = rule.RetryDelayMinutes.ToString();
        FaultTypeRuleMaxRetryCountText = rule.MaxRetryCount.ToString();
        FaultTypeRuleAutoEnabled = rule.IsAutoRecheckEnabled;
        RaisePropertyChanged(nameof(SelectedFaultTypeRuleSummaryText));
        RaisePropertyChanged(nameof(SelectedFaultTypeRuleStateText));
        RaisePropertyChanged(nameof(CanSaveFaultTypeRule));
        RaisePropertyChanged(nameof(CanRemoveFaultTypeRule));
    }

    private RecheckScheduleRule? ResolveSelectedFaultTypeRule()
    {
        if (SelectedFaultTypeRuleOption is null)
        {
            return null;
        }

        return ConfiguredFaultTypeRules.FirstOrDefault(item =>
            string.Equals(item.ScopeKey, SelectedFaultTypeRuleOption.Key, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncSelectedTaskOverrideEditor()
    {
        var rule = SelectedRecheckTask?.ScheduleRule ?? _currentRuleCatalog.GlobalDefaultRule;
        SelectedTaskOverrideEnabled = SelectedRecheckTask?.HasManualRuleOverride == true;
        SelectedTaskOverrideInitialDelayMinutesText = rule.InitialDelayMinutes.ToString();
        SelectedTaskOverrideIntervalMinutesText = rule.IntervalMinutes.ToString();
        SelectedTaskOverrideRetryDelayMinutesText = rule.RetryDelayMinutes.ToString();
        SelectedTaskOverrideMaxRetryCountText = rule.MaxRetryCount.ToString();
        SelectedTaskOverrideAutoEnabled = rule.IsAutoRecheckEnabled;
        RaisePropertyChanged(nameof(SelectedTaskRuleSourceText));
        RaisePropertyChanged(nameof(CanSaveSelectedTaskOverride));
        RaisePropertyChanged(nameof(CanClearSelectedTaskOverride));
    }

    private static bool TryParseNonNegativeInt(string value, string fieldName, out int result, out string errorMessage)
    {
        if (!int.TryParse(value, out result) || result < 0)
        {
            errorMessage = $"{fieldName}请输入大于或等于 0 的整数。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryParsePositiveInt(string value, string fieldName, out int result, out string errorMessage)
    {
        if (!int.TryParse(value, out result) || result <= 0)
        {
            errorMessage = $"{fieldName}请输入大于 0 的整数。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private RecheckTaskRecord? ResolvePreferredTask()
    {
        if (SelectedRecord is not null)
        {
            var byRecord = RecheckTasks.FirstOrDefault(item =>
                string.Equals(item.SourceFaultClosureId, SelectedRecord.RecordId, StringComparison.OrdinalIgnoreCase));
            if (byRecord is not null)
            {
                return byRecord;
            }
        }

        return RecheckTasks.FirstOrDefault();
    }

    private void SyncSelectedTaskFromRecord()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        SelectedRecheckTask = RecheckTasks.FirstOrDefault(item =>
            string.Equals(item.SourceFaultClosureId, SelectedRecord.RecordId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedRecheckTask;
    }

    private void SyncSelectedTaskExecutions()
    {
        var overview = _recheckSchedulerService.GetOverview();
        ReplaceCollection(
            SelectedTaskExecutions,
            overview.RecentExecutions
                .Where(item => SelectedRecheckTask is not null &&
                               string.Equals(item.TaskId, SelectedRecheckTask.TaskId, StringComparison.OrdinalIgnoreCase))
                .Take(12));
    }

    private void ResetActionDraft()
    {
        OperatorName = ResolveDefaultOperator();
        ActionNote = string.Empty;
    }

    private void TriggerFilterRefresh()
    {
        if (IsRefreshing || IsProcessingAction)
        {
            return;
        }

        _ = RefreshAsync(preserveSelection: true);
    }

    private void UpdateFaultTypeOptions(IReadOnlyList<string> faultTypes)
    {
        var selectedKey = SelectedFaultTypeOption?.Key ?? string.Empty;
        ReplaceCollection(FaultTypeOptions, BuildAllOption("全部故障类型")
            .Concat(faultTypes.Select(item => new SelectionItemViewModel
            {
                Key = item,
                Title = item
            })));
        SelectedFaultTypeOption = FaultTypeOptions.FirstOrDefault(item => string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase))
            ?? FaultTypeOptions.FirstOrDefault();
        UpdateFaultTypeRuleOptions();
    }

    private void UpdateSummaryCards(FaultClosureOverview overview)
    {
        ReplaceCollection(SummaryCards,
        [
            BuildMetric("当前记录", overview.TotalCount.ToString(), "条", "当前筛选结果", "ToneInfoBrush"),
            BuildMetric("待派单", overview.PendingDispatchCount.ToString(), "条", "本地草稿队列", "ToneWarningBrush"),
            BuildMetric("待复检", overview.PendingRecheckCount.ToString(), "条", "进入复检队列", "ToneDangerBrush"),
            BuildMetric("待销警", overview.PendingClearCount.ToString(), "条", "人工确认恢复", "ToneFocusBrush"),
            BuildMetric("已闭环", (overview.ClearedCount + overview.ClosedCount).ToString(), "条", "本地终态沉淀", "ToneSuccessBrush")
        ]);
    }

    private void SyncMediaReviewContext()
    {
        if (SelectedRecord is null)
        {
            _mediaReview.Clear();
            return;
        }

        var scopeDevice = _inspectionScopeService.GetCurrentScope().Devices
            .FirstOrDefault(item => string.Equals(item.Device.DeviceCode, SelectedRecord.DeviceCode, StringComparison.OrdinalIgnoreCase));
        if (scopeDevice is not null)
        {
            _mediaReview.BindTarget(
                scopeDevice.Device.DeviceCode,
                scopeDevice.Device.DeviceName,
                scopeDevice.Device.NetTypeCode,
                scopeDevice.LatestInspection);
            return;
        }

        var cachedDevice = _deviceCatalogService.GetCachedDevices()
            .FirstOrDefault(item => string.Equals(item.DeviceCode, SelectedRecord.DeviceCode, StringComparison.OrdinalIgnoreCase));
        _mediaReview.BindTarget(
            SelectedRecord.DeviceCode,
            cachedDevice?.DeviceName ?? SelectedRecord.DeviceName,
            cachedDevice?.NetTypeCode,
            _deviceInspectionService.GetLatestResult(SelectedRecord.DeviceCode));
    }

    private void RaiseSelectedRecordChanged()
    {
        RaisePropertyChanged(nameof(HasSelectedRecord));
        RaisePropertyChanged(nameof(HasSelectedImage));
        RaisePropertyChanged(nameof(CanMarkDispatched));
        RaisePropertyChanged(nameof(CanRunRecheck));
        RaisePropertyChanged(nameof(CanAddToRecheckQueue));
        RaisePropertyChanged(nameof(CanClearRecovered));
        RaisePropertyChanged(nameof(CanCloseRecord));
        RaisePropertyChanged(nameof(CanCloseFalsePositive));
        RaisePropertyChanged(nameof(SelectedImageUri));
        RaisePropertyChanged(nameof(SelectedTicketNumber));
        RaisePropertyChanged(nameof(SelectedStatusText));
        RaisePropertyChanged(nameof(SelectedSourceText));
        RaisePropertyChanged(nameof(SelectedFaultTypeText));
        RaisePropertyChanged(nameof(SelectedReviewConclusionText));
        RaisePropertyChanged(nameof(SelectedSummaryText));
        RaisePropertyChanged(nameof(SelectedPointNameText));
        RaisePropertyChanged(nameof(SelectedPointCodeText));
        RaisePropertyChanged(nameof(SelectedDirectoryText));
        RaisePropertyChanged(nameof(SelectedEvidenceSummaryText));
        RaisePropertyChanged(nameof(SelectedAiAlertText));
        RaisePropertyChanged(nameof(SelectedPlaybackText));
        RaisePropertyChanged(nameof(SelectedPrimaryEvidenceTimeText));
        RaisePropertyChanged(nameof(SelectedActionNoteText));
        RaisePropertyChanged(nameof(SelectedDispatchChannelsText));
        RaisePropertyChanged(nameof(SelectedDispatchCreatedText));
        RaisePropertyChanged(nameof(SelectedDispatchStatusText));
        RaisePropertyChanged(nameof(SelectedDispatchDispatchedText));
        RaisePropertyChanged(nameof(SelectedLatestRecheckText));
        RaisePropertyChanged(nameof(SelectedClearTrailText));
    }

    private void RaiseSelectedTaskChanged()
    {
        RaisePropertyChanged(nameof(HasSelectedTask));
        RaisePropertyChanged(nameof(CanTriggerSelectedTask));
        RaisePropertyChanged(nameof(CanToggleSelectedTaskEnabled));
        RaisePropertyChanged(nameof(SelectedTaskStatusText));
        RaisePropertyChanged(nameof(SelectedTaskNextRunText));
        RaisePropertyChanged(nameof(SelectedTaskRuleText));
        RaisePropertyChanged(nameof(SelectedTaskRuleSourceText));
        RaisePropertyChanged(nameof(SelectedTaskLatestResultText));
        RaisePropertyChanged(nameof(SelectedTaskEnabledText));
        RaisePropertyChanged(nameof(CanSaveSelectedTaskOverride));
        RaisePropertyChanged(nameof(CanClearSelectedTaskOverride));
        RaisePropertyChanged(nameof(ToggleTaskEnabledButtonText));
    }

    private static IEnumerable<SelectionItemViewModel> BuildStatusOptions()
    {
        return BuildAllOption("全部状态").Concat(
        [
            CreateOption(FaultClosureStatuses.PendingDispatch, "待派单"),
            CreateOption(FaultClosureStatuses.DispatchedPendingProcess, "已派单待处理"),
            CreateOption(FaultClosureStatuses.PendingRecheck, "待复检"),
            CreateOption(FaultClosureStatuses.RecheckPassedPendingClear, "复检通过待销警"),
            CreateOption(FaultClosureStatuses.Cleared, "已销警"),
            CreateOption(FaultClosureStatuses.Closed, "已关闭"),
            CreateOption(FaultClosureStatuses.FalsePositiveClosed, "误报关闭")
        ]);
    }

    private static IEnumerable<SelectionItemViewModel> BuildSourceOptions()
    {
        return BuildAllOption("全部来源").Concat(
        [
            CreateOption(FaultClosureSourceTypes.LiveReview, "直播复核"),
            CreateOption(FaultClosureSourceTypes.PlaybackReview, "回看复核"),
            CreateOption(FaultClosureSourceTypes.AiAlert, "AI画面异常"),
            CreateOption(FaultClosureSourceTypes.InspectionFailure, "基础巡检失败")
        ]);
    }

    private static IEnumerable<SelectionItemViewModel> BuildFaultTypeRuleOptions(IEnumerable<string> faultTypes)
    {
        var preferredFaultTypes = new[]
        {
            "黑屏",
            "冻帧",
            "偏斜",
            "遮挡",
            "模糊",
            "低照度"
        };

        return preferredFaultTypes
            .Concat(faultTypes)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(item => new SelectionItemViewModel
            {
                Key = item,
                Title = item
            });
    }

    private static IEnumerable<SelectionItemViewModel> BuildAllOption(string title)
    {
        return
        [
            new SelectionItemViewModel
            {
                Key = string.Empty,
                Title = title
            }
        ];
    }

    private static SelectionItemViewModel CreateOption(string key, string title)
    {
        return new SelectionItemViewModel
        {
            Key = key,
            Title = title
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

    private static string ResolveDefaultOperator()
    {
        return string.IsNullOrWhiteSpace(Environment.UserName)
            ? "current-operator"
            : Environment.UserName.Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
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
