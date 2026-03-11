using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class ScopeDeviceItemViewModel : ObservableObject
{
    private readonly Action<ScopeDeviceItemViewModel>? _onDraftChanged;
    private bool _isDraftIncluded;
    private bool _isDraftExcluded;
    private bool _isDraftFocused;

    public ScopeDeviceItemViewModel(
        DeviceDirectoryItem device,
        bool isInCurrentScope,
        bool isFocused,
        DeviceInspectionResult? latestInspection,
        FaultClosureLinkageSummary closureSummary,
        bool hasCoordinate,
        double? longitude,
        double? latitude,
        string coordinateSourceText,
        bool isDraftIncluded,
        bool isDraftExcluded,
        bool isDraftFocused,
        Action<ScopeDeviceItemViewModel>? onDraftChanged)
    {
        Device = device;
        IsInCurrentScope = isInCurrentScope;
        IsFocused = isFocused;
        LatestInspection = latestInspection;
        ClosureSummary = closureSummary;
        HasCoordinate = hasCoordinate;
        Longitude = longitude;
        Latitude = latitude;
        CoordinateSourceText = coordinateSourceText;
        _isDraftIncluded = isDraftIncluded;
        _isDraftExcluded = isDraftExcluded;
        _isDraftFocused = isDraftFocused;
        _onDraftChanged = onDraftChanged;
    }

    public DeviceDirectoryItem Device { get; }

    public bool IsInCurrentScope { get; }

    public bool IsFocused { get; }

    public DeviceInspectionResult? LatestInspection { get; }

    public FaultClosureLinkageSummary ClosureSummary { get; }

    public bool HasCoordinate { get; }

    public double? Longitude { get; }

    public double? Latitude { get; }

    public string CoordinateSourceText { get; }

    public bool IsDraftIncluded
    {
        get => _isDraftIncluded;
        set
        {
            if (value && _isDraftExcluded)
            {
                UpdateDraftExcluded(false);
            }

            if (SetProperty(ref _isDraftIncluded, value))
            {
                _onDraftChanged?.Invoke(this);
            }
        }
    }

    public bool IsDraftExcluded
    {
        get => _isDraftExcluded;
        set
        {
            if (value && _isDraftIncluded)
            {
                UpdateDraftIncluded(false);
            }

            if (SetProperty(ref _isDraftExcluded, value))
            {
                _onDraftChanged?.Invoke(this);
            }
        }
    }

    public bool IsDraftFocused
    {
        get => _isDraftFocused;
        set
        {
            if (SetProperty(ref _isDraftFocused, value))
            {
                _onDraftChanged?.Invoke(this);
            }
        }
    }

    public string DeviceCode => Device.DeviceCode;

    public string DeviceName => Device.DeviceName;

    public string DirectoryName => Device.DirectoryName;

    public string DirectoryPath => Device.DirectoryPath;

    public string OnlineStatusText => Device.OnlineStatusText;

    public string DeviceSourceText => Device.DeviceSourceText;

    public string NetTypeText => Device.NetTypeText;

    public string PlaybackGradeBadgeText => LatestInspection is null
        ? "待检"
        : $"等级 {LatestInspection.PlaybackHealthGrade}";

    public string PlaybackGradeSummary => LatestInspection?.PlaybackHealthSummary ?? "尚未执行基础巡检";

    public string LastInspectionTimeText => LatestInspection?.InspectionTimeText ?? "待检";

    public string LastFailureReasonSummary => LatestInspection?.FailureReasonSummary ?? "最近无失败";

    public string PreferredProtocolText => LatestInspection?.PreferredProtocolText ?? "--";

    public bool NeedRecheck => LatestInspection?.NeedRecheck == true;

    public string RecheckBadgeText => NeedRecheck ? "需复检" : "稳定";

    public bool HasClosureRecord => ClosureSummary.HasRecord;

    public string ClosureStatusText => ClosureSummary.CurrentStatusText;

    public string ClosurePendingFlagsText => ClosureSummary.PendingFlagsText;

    public string ClosureReviewConclusionText => ClosureSummary.ReviewConclusionText;

    public string ClosureLatestRecheckText => ClosureSummary.LatestRecheckText;

    public string ClosureAccentResourceKey => ClosureSummary.AccentResourceKey;

    public bool IsPendingDispatch => ClosureSummary.IsPendingDispatch;

    public bool IsPendingRecheck => ClosureSummary.IsPendingRecheck;

    public bool IsPendingClear => ClosureSummary.IsPendingClear;

    public bool IsFalsePositiveClosed => ClosureSummary.IsFalsePositiveClosed;

    public string ClosurePendingDispatchText => ClosureSummary.PendingDispatchText;

    public string ClosurePendingRecheckText => ClosureSummary.PendingRecheckText;

    public string ClosurePendingClearText => ClosureSummary.PendingClearText;

    public string CoordinateText => HasCoordinate && Longitude.HasValue && Latitude.HasValue
        ? $"{Longitude.Value:F6} / {Latitude.Value:F6}"
        : "--";

    public string ScopeBadgeText => IsFocused ? "重点关注" : IsInCurrentScope ? "方案内" : "缓存池";

    private void UpdateDraftIncluded(bool value)
    {
        if (SetProperty(ref _isDraftIncluded, value, nameof(IsDraftIncluded)))
        {
            _onDraftChanged?.Invoke(this);
        }
    }

    private void UpdateDraftExcluded(bool value)
    {
        if (SetProperty(ref _isDraftExcluded, value, nameof(IsDraftExcluded)))
        {
            _onDraftChanged?.Invoke(this);
        }
    }
}
