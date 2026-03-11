using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class ReviewCenterService : IReviewCenterService
{
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IAiAlertService _aiAlertService;
    private readonly IAiAlertStore _aiAlertStore;
    private readonly IScreenshotSampleStore _screenshotSampleStore;
    private readonly IPlaybackReviewStore _playbackReviewStore;
    private readonly IManualReviewStore _manualReviewStore;
    private readonly IFaultClosureService _faultClosureService;
    private readonly object _syncRoot = new();

    public ReviewCenterService(
        IInspectionScopeService inspectionScopeService,
        IAiAlertService aiAlertService,
        IAiAlertStore aiAlertStore,
        IScreenshotSampleStore screenshotSampleStore,
        IPlaybackReviewStore playbackReviewStore,
        IManualReviewStore manualReviewStore,
        IFaultClosureService faultClosureService)
    {
        _inspectionScopeService = inspectionScopeService;
        _aiAlertService = aiAlertService;
        _aiAlertStore = aiAlertStore;
        _screenshotSampleStore = screenshotSampleStore;
        _playbackReviewStore = playbackReviewStore;
        _manualReviewStore = manualReviewStore;
        _faultClosureService = faultClosureService;
    }

    public ReviewCenterOverview GetOverview(ReviewCenterQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = _inspectionScopeService.GetCurrentScope();
        var deviceLookup = scope.Devices
            .Where(item => !string.IsNullOrWhiteSpace(item.Device.DeviceCode))
            .GroupBy(item => item.Device.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var deviceCodes = new HashSet<string>(deviceLookup.Keys, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        TryRefreshAiAlertsIfNeeded(query, deviceCodes, warnings);

        var screenshotSamples = _screenshotSampleStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode))
            .OrderByDescending(item => item.CapturedAt)
            .ToList();
        var playbackResults = _playbackReviewStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode))
            .OrderByDescending(item => item.ReviewedAt)
            .ToList();
        var aiAlerts = _aiAlertStore.LoadAll()
            .Where(item => item.AlertType == 3 && deviceCodes.Contains(item.DeviceCode))
            .OrderByDescending(item => item.CreateTime)
            .ToList();
        var manualReviews = _manualReviewStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode))
            .OrderByDescending(item => item.ReviewedAt)
            .ToList();

        var latestManualByEvidenceId = manualReviews
            .Where(item => !string.IsNullOrWhiteSpace(item.EvidenceId))
            .GroupBy(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var playbackBySessionId = playbackResults
            .Where(item => !string.IsNullOrWhiteSpace(item.SessionId))
            .GroupBy(item => item.SessionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var latestSampleIds = screenshotSamples
            .GroupBy(item => $"{item.DeviceCode}:{NormalizeSourceKind(item.ReviewTargetKind)}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().SampleId, StringComparer.OrdinalIgnoreCase);
        var latestAiAlertIds = aiAlerts
            .GroupBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var evidenceItems = new List<ReviewEvidenceItem>();
        evidenceItems.AddRange(BuildScreenshotEvidence(
            screenshotSamples,
            latestSampleIds,
            playbackBySessionId,
            latestManualByEvidenceId,
            deviceLookup,
            scope.CurrentScheme));
        evidenceItems.AddRange(BuildAiEvidence(
            aiAlerts,
            latestAiAlertIds,
            latestManualByEvidenceId,
            deviceLookup,
            scope.CurrentScheme));

        var abnormalDeviceCodes = evidenceItems
            .Where(item => item.IsAbnormal)
            .GroupBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= 2)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalizedEvidenceItems = evidenceItems
            .Select(item => abnormalDeviceCodes.Contains(item.DeviceCode)
                ? CloneEvidence(item, isContinuousAbnormal: true)
                : item)
            .OrderByDescending(item => item.CapturedAt)
            .ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReviewCenterOverview
        {
            CurrentScheme = scope.CurrentScheme,
            ScopeSummary = scope.Summary,
            EvidenceItems = normalizedEvidenceItems,
            TotalEvidenceCount = normalizedEvidenceItems.Count,
            PendingManualCount = normalizedEvidenceItems.Count(item => item.IsPendingManualReview),
            AbnormalEvidenceCount = normalizedEvidenceItems.Count(item => item.IsAbnormal),
            AiEvidenceCount = normalizedEvidenceItems.Count(item => string.Equals(item.EvidenceKind, ReviewEvidenceKinds.AiEvidence, StringComparison.OrdinalIgnoreCase)),
            FocusedEvidenceCount = normalizedEvidenceItems.Count(item => item.IsFocused),
            ContinuousAbnormalPointCount = abnormalDeviceCodes.Count,
            GeneratedAt = DateTimeOffset.Now,
            StatusMessage = $"当前方案“{scope.CurrentScheme.Name}”内已汇总 {normalizedEvidenceItems.Count} 条截图/证据样本。",
            WarningMessage = string.Join(" ", warnings.Where(item => !string.IsNullOrWhiteSpace(item)))
        };
    }

    public ManualReviewRecord SaveManualReview(ManualReviewSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.EvidenceId))
        {
            throw new InvalidOperationException("证据标识不能为空，无法保存人工复核结论。");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceCode))
        {
            throw new InvalidOperationException("设备编码不能为空，无法保存人工复核结论。");
        }

        if (string.IsNullOrWhiteSpace(request.Reviewer))
        {
            throw new InvalidOperationException("复核人不能为空。");
        }

        var record = new ManualReviewRecord
        {
            ReviewId = Guid.NewGuid().ToString("N"),
            EvidenceId = request.EvidenceId.Trim(),
            DeviceCode = request.DeviceCode.Trim(),
            DeviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? request.DeviceCode.Trim() : request.DeviceName.Trim(),
            SchemeId = request.SchemeId.Trim(),
            SchemeName = request.SchemeName.Trim(),
            SourceKind = NormalizeSourceKind(request.SourceKind),
            Conclusion = NormalizeConclusion(request.Conclusion),
            Reviewer = request.Reviewer.Trim(),
            ReviewedAt = DateTimeOffset.Now,
            RemarkText = request.RemarkText.Trim(),
            RequiresDispatch = request.RequiresDispatch,
            RequiresRecheck = request.RequiresRecheck,
            RelatedScreenshotSampleId = request.RelatedScreenshotSampleId.Trim(),
            RelatedPlaybackReviewSessionId = request.RelatedPlaybackReviewSessionId.Trim(),
            RelatedAiAlertId = request.RelatedAiAlertId.Trim(),
            RelatedDeviceCode = string.IsNullOrWhiteSpace(request.RelatedDeviceCode)
                ? request.DeviceCode.Trim()
                : request.RelatedDeviceCode.Trim()
        };

        lock (_syncRoot)
        {
            var items = _manualReviewStore.Load().ToList();
            items.Add(record);

            _manualReviewStore.Save(items
                .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.ReviewedAt)
                .ToList());
        }

        _faultClosureService.UpsertFromManualReview(record);

        return record;
    }

    private void TryRefreshAiAlertsIfNeeded(ReviewCenterQuery query, ISet<string> deviceCodes, ICollection<string> warnings)
    {
        if (deviceCodes.Count == 0)
        {
            return;
        }

        var cachedAlerts = _aiAlertStore.LoadAll()
            .Where(item => item.AlertType == 3 && deviceCodes.Contains(item.DeviceCode))
            .ToList();
        if (!query.ForceRefreshAiAlerts && cachedAlerts.Count > 0)
        {
            return;
        }

        try
        {
            for (var pageNo = 1; pageNo <= Math.Max(1, query.AiMaxPages); pageNo += 1)
            {
                var result = _aiAlertService.Query(new AiAlertQuery
                {
                    AlertTypes = [3],
                    StartTime = DateTimeOffset.Now.AddDays(-Math.Max(1, query.AiRecentDays)),
                    EndTime = DateTimeOffset.Now,
                    PageNo = pageNo,
                    PageSize = Math.Clamp(query.AiPageSize, 10, 100)
                });

                if (result.Items.Count == 0 || !result.HasMore)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"AI画面异常刷新失败，已回退到本地缓存：{ex.Message}");
        }
    }

    private static IReadOnlyList<ReviewEvidenceItem> BuildScreenshotEvidence(
        IReadOnlyList<ScreenshotSampleResult> screenshotSamples,
        IReadOnlyDictionary<string, string> latestSampleIds,
        IReadOnlyDictionary<string, PlaybackReviewResult> playbackBySessionId,
        IReadOnlyDictionary<string, ManualReviewRecord> latestManualByEvidenceId,
        IReadOnlyDictionary<string, InspectionScopeDevice> deviceLookup,
        InspectionScopeScheme scheme)
    {
        var results = new List<ReviewEvidenceItem>(screenshotSamples.Count);

        foreach (var sample in screenshotSamples)
        {
            if (!deviceLookup.TryGetValue(sample.DeviceCode, out var scopeDevice))
            {
                continue;
            }

            var sourceKind = NormalizeSourceKind(sample.ReviewTargetKind);
            var evidenceKind = string.Equals(sourceKind, ManualReviewSourceKinds.Playback, StringComparison.OrdinalIgnoreCase)
                ? ReviewEvidenceKinds.PlaybackScreenshot
                : ReviewEvidenceKinds.LiveScreenshot;
            var evidenceRoleKey = $"{sample.DeviceCode}:{sourceKind}";
            var relatedPlayback = playbackBySessionId.GetValueOrDefault(sample.ReviewSessionId);
            var manualReview = latestManualByEvidenceId.GetValueOrDefault(sample.SampleId);
            var machineAbnormal = DetermineScreenshotAbnormal(sample, relatedPlayback, scopeDevice);

            results.Add(new ReviewEvidenceItem
            {
                EvidenceId = sample.SampleId,
                DeviceCode = sample.DeviceCode,
                DeviceName = sample.DeviceName,
                SchemeId = scheme.Id,
                SchemeName = scheme.Name,
                DirectoryName = scopeDevice.Device.DirectoryName,
                DirectoryPath = scopeDevice.Device.DirectoryPath,
                EvidenceKind = evidenceKind,
                SourceKind = sourceKind,
                EvidenceRole = latestSampleIds.GetValueOrDefault(evidenceRoleKey) == sample.SampleId
                    ? ReviewEvidenceRoles.Current
                    : ReviewEvidenceRoles.Historical,
                ImageUri = sample.ImagePath,
                CapturedAt = sample.CapturedAt,
                Protocol = sample.Protocol,
                ReviewSessionId = sample.ReviewSessionId,
                PlaybackFileId = relatedPlayback?.PlaybackFileId ?? string.Empty,
                PlaybackFileName = sample.PlaybackFileName,
                AiAlertId = string.Empty,
                AiAlertMsgId = string.Empty,
                AiAlertContent = string.Empty,
                AiAlertSourceName = string.Empty,
                PlaybackHealthGrade = scopeDevice.LatestInspection?.PlaybackHealthGrade,
                VideoEncoding = scopeDevice.LatestInspection?.VideoEnc ?? relatedPlayback?.VideoEncoding ?? string.Empty,
                FailureReason = relatedPlayback?.FailureReason ?? scopeDevice.LatestInspection?.FailureReason ?? string.Empty,
                IsFocused = scopeDevice.IsFocused,
                NeedRecheck = scopeDevice.NeedRecheck,
                IsMachineAbnormal = machineAbnormal,
                IsAbnormal = ApplyManualOverride(machineAbnormal, manualReview),
                IsPendingManualReview = manualReview is null || manualReview.IsPending,
                IsContinuousAbnormal = false,
                ManualReviewConclusion = manualReview?.Conclusion ?? ManualReviewConclusions.Pending,
                ManualReviewReviewer = manualReview?.Reviewer ?? string.Empty,
                ManualReviewedAt = manualReview?.ReviewedAt,
                ManualReviewRemark = manualReview?.RemarkText ?? string.Empty,
                RequiresDispatch = manualReview?.RequiresDispatch ?? false,
                RequiresRecheck = manualReview?.RequiresRecheck ?? false
            });
        }

        return results;
    }

    private static IReadOnlyList<ReviewEvidenceItem> BuildAiEvidence(
        IReadOnlyList<AiAlertDetail> aiAlerts,
        IReadOnlyDictionary<string, string> latestAiAlertIds,
        IReadOnlyDictionary<string, ManualReviewRecord> latestManualByEvidenceId,
        IReadOnlyDictionary<string, InspectionScopeDevice> deviceLookup,
        InspectionScopeScheme scheme)
    {
        var results = new List<ReviewEvidenceItem>(aiAlerts.Count);

        foreach (var alert in aiAlerts)
        {
            if (!deviceLookup.TryGetValue(alert.DeviceCode, out var scopeDevice))
            {
                continue;
            }

            var manualReview = latestManualByEvidenceId.GetValueOrDefault(alert.Id);

            results.Add(new ReviewEvidenceItem
            {
                EvidenceId = alert.Id,
                DeviceCode = alert.DeviceCode,
                DeviceName = alert.DeviceName,
                SchemeId = scheme.Id,
                SchemeName = scheme.Name,
                DirectoryName = scopeDevice.Device.DirectoryName,
                DirectoryPath = scopeDevice.Device.DirectoryPath,
                EvidenceKind = ReviewEvidenceKinds.AiEvidence,
                SourceKind = ManualReviewSourceKinds.Ai,
                EvidenceRole = latestAiAlertIds.GetValueOrDefault(alert.DeviceCode) == alert.Id
                    ? ReviewEvidenceRoles.Current
                    : ReviewEvidenceRoles.Historical,
                ImageUri = ResolveAiImageUri(alert),
                CapturedAt = alert.CreateTime,
                Protocol = string.Empty,
                ReviewSessionId = string.Empty,
                PlaybackFileId = alert.CloudFileId ?? string.Empty,
                PlaybackFileName = alert.CloudFileName ?? string.Empty,
                AiAlertId = alert.Id,
                AiAlertMsgId = alert.MsgId,
                AiAlertContent = alert.Content,
                AiAlertSourceName = alert.AlertSourceName,
                PlaybackHealthGrade = scopeDevice.LatestInspection?.PlaybackHealthGrade,
                VideoEncoding = scopeDevice.LatestInspection?.VideoEnc ?? string.Empty,
                FailureReason = scopeDevice.LatestInspection?.FailureReason ?? alert.Summary,
                IsFocused = scopeDevice.IsFocused,
                NeedRecheck = true,
                IsMachineAbnormal = true,
                IsAbnormal = ApplyManualOverride(true, manualReview),
                IsPendingManualReview = manualReview is null || manualReview.IsPending,
                IsContinuousAbnormal = false,
                ManualReviewConclusion = manualReview?.Conclusion ?? ManualReviewConclusions.Pending,
                ManualReviewReviewer = manualReview?.Reviewer ?? string.Empty,
                ManualReviewedAt = manualReview?.ReviewedAt,
                ManualReviewRemark = manualReview?.RemarkText ?? string.Empty,
                RequiresDispatch = manualReview?.RequiresDispatch ?? false,
                RequiresRecheck = manualReview?.RequiresRecheck ?? true
            });
        }

        return results;
    }

    private static ReviewEvidenceItem CloneEvidence(ReviewEvidenceItem item, bool isContinuousAbnormal)
    {
        return new ReviewEvidenceItem
        {
            EvidenceId = item.EvidenceId,
            DeviceCode = item.DeviceCode,
            DeviceName = item.DeviceName,
            SchemeId = item.SchemeId,
            SchemeName = item.SchemeName,
            DirectoryName = item.DirectoryName,
            DirectoryPath = item.DirectoryPath,
            EvidenceKind = item.EvidenceKind,
            SourceKind = item.SourceKind,
            EvidenceRole = item.EvidenceRole,
            ImageUri = item.ImageUri,
            CapturedAt = item.CapturedAt,
            Protocol = item.Protocol,
            ReviewSessionId = item.ReviewSessionId,
            PlaybackFileId = item.PlaybackFileId,
            PlaybackFileName = item.PlaybackFileName,
            AiAlertId = item.AiAlertId,
            AiAlertMsgId = item.AiAlertMsgId,
            AiAlertContent = item.AiAlertContent,
            AiAlertSourceName = item.AiAlertSourceName,
            PlaybackHealthGrade = item.PlaybackHealthGrade,
            VideoEncoding = item.VideoEncoding,
            FailureReason = item.FailureReason,
            IsFocused = item.IsFocused,
            NeedRecheck = item.NeedRecheck,
            IsMachineAbnormal = item.IsMachineAbnormal,
            IsAbnormal = item.IsAbnormal,
            IsPendingManualReview = item.IsPendingManualReview,
            IsContinuousAbnormal = isContinuousAbnormal,
            ManualReviewConclusion = item.ManualReviewConclusion,
            ManualReviewReviewer = item.ManualReviewReviewer,
            ManualReviewedAt = item.ManualReviewedAt,
            ManualReviewRemark = item.ManualReviewRemark,
            RequiresDispatch = item.RequiresDispatch,
            RequiresRecheck = item.RequiresRecheck
        };
    }

    private static string ResolveAiImageUri(AiAlertDetail alert)
    {
        return FirstNonEmpty(
            alert.SnapshotImageUrl,
            alert.ThumbnailImageUrl,
            alert.BackgroundImageUrl,
            alert.CloudFileIconUrl,
            alert.DownloadUrl);
    }

    private static bool DetermineScreenshotAbnormal(
        ScreenshotSampleResult sample,
        PlaybackReviewResult? relatedPlayback,
        InspectionScopeDevice scopeDevice)
    {
        if (relatedPlayback is { FirstFrameVisible: false })
        {
            return true;
        }

        if (string.Equals(sample.ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase) &&
            relatedPlayback is { PlaybackStarted: false })
        {
            return true;
        }

        return scopeDevice.LatestInspection?.IsAbnormal == true || scopeDevice.NeedRecheck;
    }

    private static bool ApplyManualOverride(bool machineAbnormal, ManualReviewRecord? manualReview)
    {
        if (manualReview is null || manualReview.IsPending)
        {
            return machineAbnormal;
        }

        return manualReview.Conclusion switch
        {
            ManualReviewConclusions.Normal => false,
            ManualReviewConclusions.FalsePositive => false,
            _ => true
        };
    }

    private static string NormalizeSourceKind(string? sourceKind)
    {
        if (string.Equals(sourceKind, ManualReviewSourceKinds.Playback, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceKind, "Playback", StringComparison.OrdinalIgnoreCase))
        {
            return ManualReviewSourceKinds.Playback;
        }

        if (string.Equals(sourceKind, ManualReviewSourceKinds.Ai, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceKind, "AI", StringComparison.OrdinalIgnoreCase))
        {
            return ManualReviewSourceKinds.Ai;
        }

        return ManualReviewSourceKinds.Live;
    }

    private static string NormalizeConclusion(string? conclusion)
    {
        return conclusion switch
        {
            ManualReviewConclusions.Normal => ManualReviewConclusions.Normal,
            ManualReviewConclusions.BlackScreen => ManualReviewConclusions.BlackScreen,
            ManualReviewConclusions.FrozenFrame => ManualReviewConclusions.FrozenFrame,
            ManualReviewConclusions.Tilted => ManualReviewConclusions.Tilted,
            ManualReviewConclusions.Obstruction => ManualReviewConclusions.Obstruction,
            ManualReviewConclusions.Blur => ManualReviewConclusions.Blur,
            ManualReviewConclusions.LowLight => ManualReviewConclusions.LowLight,
            ManualReviewConclusions.FalsePositive => ManualReviewConclusions.FalsePositive,
            _ => ManualReviewConclusions.Pending
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
