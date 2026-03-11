using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.UI.ViewModels;

public sealed class DeviceMediaReviewViewModel : ObservableObject
{
    private readonly IPlaybackReviewService _playbackReviewService;
    private readonly IScreenshotSamplingService _screenshotSamplingService;
    private readonly ICloudPlaybackService _cloudPlaybackService;

    private string _deviceCode = string.Empty;
    private string _deviceName = string.Empty;
    private int? _netTypeCode;
    private DeviceInspectionResult? _latestInspectionResult;
    private PlaybackReviewSession? _currentSession;
    private PlaybackReviewResult? _latestReviewResult;
    private ScreenshotSampleResult? _latestScreenshot;
    private CloudPlaybackFile? _selectedPlaybackFile;
    private string _liveReviewStatusText = "请选择点位后加载直播复核。";
    private string _liveReviewAlertText = string.Empty;
    private string _playbackFileStatusText = "当前尚未查询回看文件。";
    private string _playbackFileAlertText = string.Empty;
    private string _activeProtocolText = "--";
    private string _activeUrlText = "--";
    private string _activeExpireTimeText = "--";
    private string _videoEncodingText = "--";
    private bool _isPreparingLiveReview;
    private bool _isPreparingPlaybackReview;
    private bool _isRefreshingPlaybackFiles;
    private string _completedSessionId = string.Empty;

    public DeviceMediaReviewViewModel(
        IPlaybackReviewService playbackReviewService,
        IScreenshotSamplingService screenshotSamplingService,
        ICloudPlaybackService cloudPlaybackService)
    {
        _playbackReviewService = playbackReviewService;
        _screenshotSamplingService = screenshotSamplingService;
        _cloudPlaybackService = cloudPlaybackService;

        RecentPlaybackFiles = new ObservableCollection<CloudPlaybackFile>();
        PrepareLiveReviewCommand = new RelayCommand<object?>(_ => _ = PrepareLiveReviewAsync(forceRefresh: false));
        RefreshLiveReviewCommand = new RelayCommand<object?>(_ => _ = PrepareLiveReviewAsync(forceRefresh: true));
        RefreshPlaybackFilesCommand = new RelayCommand<object?>(_ => _ = LoadRecentPlaybackFilesAsync(forceRefresh: true));
        PlayPlaybackFileCommand = new RelayCommand<CloudPlaybackFile>(file => _ = PreparePlaybackReviewAsync(file, forceRefresh: false));
        RefreshPlaybackStreamCommand = new RelayCommand<CloudPlaybackFile>(file => _ = PreparePlaybackReviewAsync(file, forceRefresh: true));
    }

    public ObservableCollection<CloudPlaybackFile> RecentPlaybackFiles { get; }

    public PlaybackReviewSession? CurrentSession
    {
        get => _currentSession;
        private set
        {
            if (SetProperty(ref _currentSession, value))
            {
                RaisePropertyChanged(nameof(HasSession));
                RaisePropertyChanged(nameof(LoadLiveReviewButtonText));
                RaisePropertyChanged(nameof(CurrentReviewTargetText));
                RaisePropertyChanged(nameof(CurrentReviewSubjectText));
            }
        }
    }

    public PlaybackReviewResult? LatestReviewResult
    {
        get => _latestReviewResult;
        private set
        {
            if (SetProperty(ref _latestReviewResult, value))
            {
                RaisePropertyChanged(nameof(HasLatestReview));
            }
        }
    }

    public ScreenshotSampleResult? LatestScreenshot
    {
        get => _latestScreenshot;
        private set
        {
            if (SetProperty(ref _latestScreenshot, value))
            {
                RaisePropertyChanged(nameof(HasLatestScreenshot));
            }
        }
    }

    public CloudPlaybackFile? SelectedPlaybackFile
    {
        get => _selectedPlaybackFile;
        private set
        {
            if (SetProperty(ref _selectedPlaybackFile, value))
            {
                RaisePropertyChanged(nameof(HasSelectedPlaybackFile));
            }
        }
    }

    public string LiveReviewStatusText
    {
        get => _liveReviewStatusText;
        private set => SetProperty(ref _liveReviewStatusText, value);
    }

    public string LiveReviewAlertText
    {
        get => _liveReviewAlertText;
        private set => SetProperty(ref _liveReviewAlertText, value);
    }

    public string PlaybackFileStatusText
    {
        get => _playbackFileStatusText;
        private set => SetProperty(ref _playbackFileStatusText, value);
    }

    public string PlaybackFileAlertText
    {
        get => _playbackFileAlertText;
        private set => SetProperty(ref _playbackFileAlertText, value);
    }

    public string ActiveProtocolText
    {
        get => _activeProtocolText;
        private set => SetProperty(ref _activeProtocolText, value);
    }

    public string ActiveUrlText
    {
        get => _activeUrlText;
        private set => SetProperty(ref _activeUrlText, value);
    }

    public string ActiveExpireTimeText
    {
        get => _activeExpireTimeText;
        private set => SetProperty(ref _activeExpireTimeText, value);
    }

    public string VideoEncodingText
    {
        get => _videoEncodingText;
        private set => SetProperty(ref _videoEncodingText, value);
    }

    public bool IsPreparingLiveReview
    {
        get => _isPreparingLiveReview;
        private set
        {
            if (SetProperty(ref _isPreparingLiveReview, value))
            {
                RaisePropertyChanged(nameof(CanPrepareLiveReview));
                RaisePropertyChanged(nameof(CanPreparePlaybackReview));
                RaisePropertyChanged(nameof(LoadLiveReviewButtonText));
            }
        }
    }

    public bool IsPreparingPlaybackReview
    {
        get => _isPreparingPlaybackReview;
        private set
        {
            if (SetProperty(ref _isPreparingPlaybackReview, value))
            {
                RaisePropertyChanged(nameof(CanPrepareLiveReview));
                RaisePropertyChanged(nameof(CanPreparePlaybackReview));
                RaisePropertyChanged(nameof(CanRefreshPlaybackFiles));
            }
        }
    }

    public bool IsRefreshingPlaybackFiles
    {
        get => _isRefreshingPlaybackFiles;
        private set
        {
            if (SetProperty(ref _isRefreshingPlaybackFiles, value))
            {
                RaisePropertyChanged(nameof(CanRefreshPlaybackFiles));
            }
        }
    }

    public bool HasTargetDevice => !string.IsNullOrWhiteSpace(_deviceCode);

    public bool HasSession => CurrentSession?.HasSources == true;

    public bool HasLatestReview => LatestReviewResult is not null;

    public bool HasLatestScreenshot => LatestScreenshot is not null;

    public bool HasRecentPlaybackFiles => RecentPlaybackFiles.Count > 0;

    public bool HasSelectedPlaybackFile => SelectedPlaybackFile is not null;

    public bool CanPrepareLiveReview => HasTargetDevice && !IsPreparingLiveReview && !IsPreparingPlaybackReview;

    public bool CanPreparePlaybackReview => HasTargetDevice && !IsPreparingLiveReview && !IsPreparingPlaybackReview;

    public bool CanRefreshPlaybackFiles => HasTargetDevice && !IsRefreshingPlaybackFiles && !IsPreparingPlaybackReview;

    public string PlayerImplementationText => "WebView2 / HTML5 / WebRTC";

    public string CurrentReviewTargetText => CurrentSession?.ReviewTargetText ?? "待复核";

    public string CurrentReviewSubjectText => CurrentSession?.ReviewSubjectText ?? NormalizeText(_deviceName, "--");

    public string LoadLiveReviewButtonText => IsPreparingLiveReview
        ? "加载中..."
        : CurrentSession is { ReviewTargetKind: "Live" }
            ? "重新加载直播"
            : "加载直播复核";

    public ICommand PrepareLiveReviewCommand { get; }

    public ICommand RefreshLiveReviewCommand { get; }

    public ICommand RefreshPlaybackFilesCommand { get; }

    public ICommand PlayPlaybackFileCommand { get; }

    public ICommand RefreshPlaybackStreamCommand { get; }

    public void BindTarget(string? deviceCode, string? deviceName, int? netTypeCode, DeviceInspectionResult? latestInspectionResult)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            Clear();
            return;
        }

        _deviceCode = deviceCode.Trim();
        _deviceName = string.IsNullOrWhiteSpace(deviceName) ? _deviceCode : deviceName.Trim();
        _netTypeCode = netTypeCode;
        _latestInspectionResult = latestInspectionResult;
        _completedSessionId = string.Empty;

        CurrentSession = null;
        SelectedPlaybackFile = null;
        LatestReviewResult = _playbackReviewService.GetLatestResult(_deviceCode);
        LatestScreenshot = _screenshotSamplingService.GetLatestSample(_deviceCode);

        var cached = _cloudPlaybackService.GetCachedRecentFiles(_deviceCode);
        ReplaceRecentFiles(cached?.Files ?? Array.Empty<CloudPlaybackFile>());
        PlaybackFileStatusText = cached is null
            ? "当前尚未加载回看文件，可手动刷新。"
            : $"已加载最近回看缓存：{cached.CachedAt:MM-dd HH:mm}";
        PlaybackFileAlertText = string.Empty;

        ResetActiveFieldsFromInspection();

        LiveReviewStatusText = LatestReviewResult is null
            ? $"当前点位“{_deviceName}”已就绪，可加载直播或回看复核。"
            : $"最近复核：{LatestReviewResult.ReviewTargetText} / {LatestReviewResult.ReviewOutcomeText} / {LatestReviewResult.StartupDurationText}";
        LiveReviewAlertText = LatestReviewResult is { FirstFrameVisible: false }
            ? LatestReviewResult.FailureReasonText
            : string.Empty;

        RaisePropertyChanged(nameof(HasTargetDevice));
        RaisePropertyChanged(nameof(CanPrepareLiveReview));
        RaisePropertyChanged(nameof(CanPreparePlaybackReview));
        RaisePropertyChanged(nameof(CanRefreshPlaybackFiles));
        RaisePropertyChanged(nameof(HasRecentPlaybackFiles));

        _ = LoadRecentPlaybackFilesAsync(forceRefresh: false);
    }

    public void Clear()
    {
        _deviceCode = string.Empty;
        _deviceName = string.Empty;
        _netTypeCode = null;
        _latestInspectionResult = null;
        _completedSessionId = string.Empty;

        CurrentSession = null;
        LatestReviewResult = null;
        LatestScreenshot = null;
        SelectedPlaybackFile = null;
        ReplaceRecentFiles(Array.Empty<CloudPlaybackFile>());

        LiveReviewStatusText = "请选择点位后加载直播复核。";
        LiveReviewAlertText = string.Empty;
        PlaybackFileStatusText = "当前尚未查询回看文件。";
        PlaybackFileAlertText = string.Empty;
        ActiveProtocolText = "--";
        ActiveUrlText = "--";
        ActiveExpireTimeText = "--";
        VideoEncodingText = "--";

        RaisePropertyChanged(nameof(HasTargetDevice));
        RaisePropertyChanged(nameof(CanPrepareLiveReview));
        RaisePropertyChanged(nameof(CanPreparePlaybackReview));
        RaisePropertyChanged(nameof(CanRefreshPlaybackFiles));
        RaisePropertyChanged(nameof(HasRecentPlaybackFiles));
    }

    public void ReportPlaybackAttempted(string sessionId, string protocol, string url, bool usedFallback)
    {
        if (!IsCurrentSession(sessionId))
        {
            return;
        }

        ActiveProtocolText = NormalizeText(protocol);
        ActiveUrlText = SensitiveDataMasker.MaskUrl(url);
        ActiveExpireTimeText = ResolveSourceExpireTimeText(protocol, url);
        LiveReviewStatusText = usedFallback
            ? $"主流不可用，正在回退到 {ActiveProtocolText} 继续{CurrentReviewTargetText}。"
            : $"正在尝试 {ActiveProtocolText} {CurrentReviewTargetText}...";
        LiveReviewAlertText = string.Empty;
    }

    public void ReportPlaybackStarted(string sessionId, string protocol, string url, bool usedFallback)
    {
        if (!IsCurrentSession(sessionId))
        {
            return;
        }

        ActiveProtocolText = NormalizeText(protocol);
        ActiveUrlText = SensitiveDataMasker.MaskUrl(url);
        ActiveExpireTimeText = ResolveSourceExpireTimeText(protocol, url);
        LiveReviewStatusText = usedFallback
            ? $"{ActiveProtocolText} 备用地址已起播，正在等待首帧。"
            : $"{ActiveProtocolText} 已起播，正在等待首帧。";
    }

    public void ReportPlaybackFailed(string sessionId, string protocol, string url, bool usedFallback, string reason)
    {
        if (!IsCurrentSession(sessionId) || HasCompletedCurrentSession(sessionId))
        {
            return;
        }

        ActiveProtocolText = NormalizeText(protocol);
        ActiveUrlText = SensitiveDataMasker.MaskUrl(url);
        ActiveExpireTimeText = ResolveSourceExpireTimeText(protocol, url);

        var result = _playbackReviewService.CompleteReview(new PlaybackReviewOutcome
        {
            SessionId = sessionId,
            ReviewTargetKind = CurrentSession?.ReviewTargetKind ?? "Live",
            DeviceCode = _deviceCode,
            DeviceName = _deviceName,
            PlaybackFileId = CurrentSession?.PlaybackFileId ?? string.Empty,
            PlaybackFileName = CurrentSession?.PlaybackFileName ?? string.Empty,
            ReviewedAt = DateTimeOffset.Now,
            PlaybackStarted = false,
            FirstFrameVisible = false,
            UsedProtocol = protocol,
            UsedUrl = url,
            UsedFallback = usedFallback,
            FailureReason = NormalizeText(reason, "轻量播放器宿主未获取到首帧。"),
            VideoEncoding = VideoEncodingText
        });

        _completedSessionId = sessionId;
        LatestReviewResult = result;
        LiveReviewStatusText = $"{result.ReviewTargetText}失败。";
        LiveReviewAlertText = result.FailureReasonText;
    }

    public void ReportFirstFrameVisible(string sessionId, string protocol, string url, bool usedFallback, int startupDurationMs)
    {
        if (!IsCurrentSession(sessionId) || HasCompletedCurrentSession(sessionId))
        {
            return;
        }

        ActiveProtocolText = NormalizeText(protocol);
        ActiveUrlText = SensitiveDataMasker.MaskUrl(url);
        ActiveExpireTimeText = ResolveSourceExpireTimeText(protocol, url);

        var result = _playbackReviewService.CompleteReview(new PlaybackReviewOutcome
        {
            SessionId = sessionId,
            ReviewTargetKind = CurrentSession?.ReviewTargetKind ?? "Live",
            DeviceCode = _deviceCode,
            DeviceName = _deviceName,
            PlaybackFileId = CurrentSession?.PlaybackFileId ?? string.Empty,
            PlaybackFileName = CurrentSession?.PlaybackFileName ?? string.Empty,
            ReviewedAt = DateTimeOffset.Now,
            PlaybackStarted = true,
            FirstFrameVisible = true,
            StartupDurationMs = startupDurationMs,
            UsedProtocol = protocol,
            UsedUrl = url,
            UsedFallback = usedFallback,
            FailureReason = string.Empty,
            VideoEncoding = VideoEncodingText
        });

        _completedSessionId = sessionId;
        LatestReviewResult = result;
        LiveReviewStatusText = usedFallback
            ? $"{result.ReviewTargetText}成功，已回退到 {result.UsedProtocol}，首帧耗时 {result.StartupDurationText}。"
            : $"{result.ReviewTargetText}成功，首帧耗时 {result.StartupDurationText}。";
        LiveReviewAlertText = string.Empty;
    }

    public void ReportHostMessage(string sessionId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sessionId) && !IsCurrentSession(sessionId))
        {
            return;
        }

        LiveReviewStatusText = message.Trim();
    }

    public async Task SaveScreenshotAsync(string sessionId, string protocol, string url, byte[] imageBytes)
    {
        if (!IsCurrentSession(sessionId) || imageBytes.Length == 0)
        {
            return;
        }

        var currentDeviceCode = _deviceCode;
        var currentSession = CurrentSession;
        var result = await Task.Run(() => _screenshotSamplingService.SaveSample(new ScreenshotSampleRequest
        {
            ReviewSessionId = sessionId,
            ReviewTargetKind = currentSession?.ReviewTargetKind ?? "Live",
            DeviceCode = currentDeviceCode,
            DeviceName = _deviceName,
            PlaybackFileName = currentSession?.PlaybackFileName ?? string.Empty,
            Protocol = protocol,
            SourceUrl = url,
            CapturedAt = DateTimeOffset.Now,
            ImageBytes = imageBytes
        }));

        if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase) ||
            !IsCurrentSession(sessionId))
        {
            return;
        }

        LatestScreenshot = result;
    }

    private async Task PrepareLiveReviewAsync(bool forceRefresh)
    {
        if (!HasTargetDevice || IsPreparingLiveReview || IsPreparingPlaybackReview)
        {
            return;
        }

        var currentDeviceCode = _deviceCode;
        IsPreparingLiveReview = true;
        SelectedPlaybackFile = null;
        LiveReviewAlertText = string.Empty;
        LiveReviewStatusText = forceRefresh
            ? "正在刷新直播地址并准备复核..."
            : "正在准备轻量播放器宿主...";

        try
        {
            var session = await Task.Run(() => _playbackReviewService.PrepareLiveReview(new PlaybackReviewPreparationRequest
            {
                DeviceCode = currentDeviceCode,
                DeviceName = _deviceName,
                NetTypeCode = _netTypeCode,
                BaseInspectionResult = _latestInspectionResult,
                ForceRefresh = forceRefresh
            }));

            if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _completedSessionId = string.Empty;
            ApplySession(session);
        }
        catch (Exception ex)
        {
            if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CurrentSession = null;
            LiveReviewStatusText = "直播复核准备失败。";
            LiveReviewAlertText = ex.Message;
        }
        finally
        {
            if (string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                IsPreparingLiveReview = false;
            }
        }
    }

    private async Task PreparePlaybackReviewAsync(CloudPlaybackFile? file, bool forceRefresh)
    {
        if (!HasTargetDevice || file is null || IsPreparingPlaybackReview || IsPreparingLiveReview)
        {
            return;
        }

        var currentDeviceCode = _deviceCode;
        IsPreparingPlaybackReview = true;
        SelectedPlaybackFile = file;
        PlaybackFileAlertText = string.Empty;
        PlaybackFileStatusText = forceRefresh
            ? $"正在刷新回看流化地址：{file.Name}"
            : $"正在准备回看复核：{file.Name}";
        LiveReviewAlertText = string.Empty;
        LiveReviewStatusText = "正在准备回看复核...";

        try
        {
            var resolved = await Task.Run(() => _cloudPlaybackService.ResolveFileStreams(
                currentDeviceCode,
                _deviceName,
                _netTypeCode,
                file,
                forceRefresh));

            if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UpdateRecentFile(resolved.File);
            SelectedPlaybackFile = resolved.File;

            var session = _playbackReviewService.PreparePlaybackReview(new PlaybackReviewPlaybackRequest
            {
                DeviceCode = currentDeviceCode,
                DeviceName = _deviceName,
                PlaybackFileId = resolved.File.Id,
                PlaybackFileName = resolved.File.Name,
                VideoEncoding = _latestInspectionResult?.VideoEncText ?? VideoEncodingText,
                HlsStreamUrl = resolved.File.HlsStreamUrl ?? string.Empty,
                RtmpStreamUrl = resolved.File.RtmpStreamUrl ?? string.Empty,
                DownloadUrl = resolved.File.DownloadUrl ?? string.Empty,
                AddressRefreshed = !resolved.FromCache,
                DiagnosticMessage = resolved.DiagnosticMessage
            });

            _completedSessionId = string.Empty;
            ApplySession(session);
            PlaybackFileStatusText = resolved.FromCache
                ? $"已复用回看流化地址：{resolved.File.Name}"
                : $"已刷新回看流化地址：{resolved.File.Name}";
            PlaybackFileAlertText = resolved.DiagnosticMessage;
        }
        catch (Exception ex)
        {
            if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PlaybackFileStatusText = "回看复核准备失败。";
            PlaybackFileAlertText = ex.Message;
            LiveReviewStatusText = "回看复核准备失败。";
            LiveReviewAlertText = ex.Message;
        }
        finally
        {
            if (string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                IsPreparingPlaybackReview = false;
            }
        }
    }

    private async Task LoadRecentPlaybackFilesAsync(bool forceRefresh)
    {
        if (!HasTargetDevice || IsRefreshingPlaybackFiles)
        {
            return;
        }

        var currentDeviceCode = _deviceCode;
        IsRefreshingPlaybackFiles = true;
        PlaybackFileAlertText = string.Empty;
        PlaybackFileStatusText = forceRefresh
            ? "正在刷新最近回看文件..."
            : "正在加载最近回看文件...";

        try
        {
            var result = await Task.Run(() => _cloudPlaybackService.GetRecentFiles(
                currentDeviceCode,
                _deviceName,
                _netTypeCode,
                forceRefresh));

            if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ReplaceRecentFiles(result.Files);
            PlaybackFileStatusText = result.Files.Count == 0
                ? result.FromCache
                    ? "缓存中暂时无最近回看文件。"
                    : "当前未查询到最近回看文件。"
                : $"{(result.FromCache ? "已加载缓存" : "已刷新")}最近回看 {result.Files.Count} 条 / {result.QueriedAt:MM-dd HH:mm}";
        }
        catch (Exception ex)
        {
            if (!string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PlaybackFileStatusText = "回看文件查询失败。";
            PlaybackFileAlertText = ex.Message;
        }
        finally
        {
            if (string.Equals(currentDeviceCode, _deviceCode, StringComparison.OrdinalIgnoreCase))
            {
                IsRefreshingPlaybackFiles = false;
            }
        }
    }

    private void ApplySession(PlaybackReviewSession session)
    {
        CurrentSession = session;
        VideoEncodingText = NormalizeText(session.VideoEncoding, _latestInspectionResult?.VideoEncText ?? "未知");

        var activeSource = session.Sources.FirstOrDefault();
        ActiveProtocolText = activeSource?.Protocol ?? "--";
        ActiveUrlText = activeSource?.DisplayUrl ?? SensitiveDataMasker.MaskUrl(_latestInspectionResult?.PreferredUrl);
        ActiveExpireTimeText = ResolveSessionAddressText(session, activeSource);

        LiveReviewStatusText = session.HasSources
            ? session.RefreshReason
            : $"未获取到可用于{session.ReviewTargetText}的地址。";
        LiveReviewAlertText = session.DiagnosticMessage;
    }

    private void ResetActiveFieldsFromInspection()
    {
        VideoEncodingText = _latestInspectionResult?.VideoEncText ?? "未知";
        ActiveProtocolText = _latestInspectionResult?.PreferredProtocolText ?? "--";
        ActiveUrlText = SensitiveDataMasker.MaskUrl(_latestInspectionResult?.PreferredUrl);
        ActiveExpireTimeText = _latestInspectionResult?.ExpireTimeText ?? "--";
    }

    private string ResolveSourceExpireTimeText(string protocol, string url)
    {
        var source = CurrentSession?.Sources.FirstOrDefault(item =>
            string.Equals(item.Protocol, protocol, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase));

        return ResolveSessionAddressText(CurrentSession, source);
    }

    private string ResolveSessionAddressText(PlaybackReviewSession? session, PlaybackReviewSource? source)
    {
        if (source?.ExpireTime is not null)
        {
            return source.ExpireTimeText;
        }

        if (session is { ReviewTargetKind: "Playback" })
        {
            return SelectedPlaybackFile?.StreamResolvedAtText is { Length: > 0 } value && value != "--"
                ? $"{value} / 按需刷新"
                : "按需刷新";
        }

        return session?.InspectionExpireTimeText
            ?? _latestInspectionResult?.ExpireTimeText
            ?? "--";
    }

    private bool IsCurrentSession(string sessionId)
    {
        return CurrentSession is not null &&
               !string.IsNullOrWhiteSpace(sessionId) &&
               string.Equals(CurrentSession.SessionId, sessionId, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasCompletedCurrentSession(string sessionId)
    {
        return !string.IsNullOrWhiteSpace(_completedSessionId) &&
               string.Equals(_completedSessionId, sessionId, StringComparison.OrdinalIgnoreCase);
    }

    private void ReplaceRecentFiles(IEnumerable<CloudPlaybackFile> files)
    {
        RecentPlaybackFiles.Clear();
        foreach (var file in files)
        {
            RecentPlaybackFiles.Add(file);
        }

        if (SelectedPlaybackFile is not null)
        {
            SelectedPlaybackFile = RecentPlaybackFiles.FirstOrDefault(item =>
                string.Equals(item.Id, SelectedPlaybackFile.Id, StringComparison.OrdinalIgnoreCase));
        }

        RaisePropertyChanged(nameof(HasRecentPlaybackFiles));
    }

    private void UpdateRecentFile(CloudPlaybackFile file)
    {
        for (var index = 0; index < RecentPlaybackFiles.Count; index += 1)
        {
            if (!string.Equals(RecentPlaybackFiles[index].Id, file.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RecentPlaybackFiles[index] = file;
            return;
        }

        RecentPlaybackFiles.Insert(0, file);
        RaisePropertyChanged(nameof(HasRecentPlaybackFiles));
    }

    private static string NormalizeText(string? value, string fallback = "--")
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
