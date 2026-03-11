using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class FaultClosureService : IFaultClosureService
{
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IFaultClosureStore _store;
    private readonly IManualReviewStore _manualReviewStore;
    private readonly IScreenshotSampleStore _screenshotSampleStore;
    private readonly IPlaybackReviewStore _playbackReviewStore;
    private readonly IAiAlertService _aiAlertService;
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly object _syncRoot = new();

    public FaultClosureService(
        IFaultClosureStore store,
        IManualReviewStore manualReviewStore,
        IScreenshotSampleStore screenshotSampleStore,
        IPlaybackReviewStore playbackReviewStore,
        IAiAlertService aiAlertService,
        IInspectionScopeService inspectionScopeService,
        IDeviceCatalogService deviceCatalogService,
        IDeviceInspectionService deviceInspectionService)
    {
        _store = store;
        _manualReviewStore = manualReviewStore;
        _screenshotSampleStore = screenshotSampleStore;
        _playbackReviewStore = playbackReviewStore;
        _aiAlertService = aiAlertService;
        _inspectionScopeService = inspectionScopeService;
        _deviceCatalogService = deviceCatalogService;
        _deviceInspectionService = deviceInspectionService;
    }

    public FaultClosureOverview GetOverview(FaultClosureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var filtered = ApplyFilters(records, query)
                .OrderBy(item => item.IsTerminal)
                .ThenByDescending(item => item.IsAwaitingRecheck)
                .ThenByDescending(item => item.IsPendingDispatch)
                .ThenByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new FaultClosureOverview
            {
                Records = filtered,
                FaultTypes = records
                    .Select(item => item.FaultType)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                TotalCount = filtered.Count,
                PendingDispatchCount = filtered.Count(item => item.IsPendingDispatch),
                PendingRecheckCount = filtered.Count(item => item.IsAwaitingRecheck),
                PendingClearCount = filtered.Count(item => string.Equals(item.CurrentStatus, FaultClosureStatuses.RecheckPassedPendingClear, StringComparison.OrdinalIgnoreCase)),
                ClearedCount = filtered.Count(item => string.Equals(item.CurrentStatus, FaultClosureStatuses.Cleared, StringComparison.OrdinalIgnoreCase)),
                ClosedCount = filtered.Count(item =>
                    string.Equals(item.CurrentStatus, FaultClosureStatuses.Closed, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.CurrentStatus, FaultClosureStatuses.FalsePositiveClosed, StringComparison.OrdinalIgnoreCase)),
                GeneratedAt = DateTimeOffset.Now,
                StatusMessage = $"当前已汇聚 {filtered.Count} 条本地闭环记录，复核、派单草稿、复检与销警状态已联动。",
                WarningMessage = string.Empty
            };
        }
    }

    public FaultClosureRecord UpsertFromManualReview(ManualReviewRecord review)
    {
        ArgumentNullException.ThrowIfNull(review);

        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var context = BuildManualReviewContext();
            var updated = UpsertManualReviewCore(records, review, context, forceApply: true);
            SaveRecords(records);
            UpdateAiWorkflow(updated);
            return updated;
        }
    }

    public FaultClosureRecord MarkDispatched(string recordId, string operatorName, string noteText)
    {
        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var record = FindRecord(records, recordId);
            if (!record.CanMarkDispatched)
            {
                throw new InvalidOperationException("当前闭环状态不允许标记为已派单。");
            }

            var updatedAt = DateTimeOffset.Now;
            var updated = BuildRecordWithDispatch(
                record,
                FaultClosureStateMachine.ResolveAfterDispatch(),
                NormalizeOperator(operatorName),
                updatedAt,
                NormalizeNote(noteText, "本地派单草稿已转入已派单待处理。"));

            ReplaceRecord(records, updated);
            SaveRecords(records);
            UpdateAiWorkflow(updated);
            return updated;
        }
    }

    public FaultClosureRecord RunRecheck(string recordId, string operatorName)
    {
        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var record = FindRecord(records, recordId);
            if (!record.CanRunRecheck)
            {
                throw new InvalidOperationException("当前记录未进入待复检队列，无法执行复检。");
            }

            var profile = ResolveDeviceProfile(record);
            var inspectionResult = _deviceInspectionService.Inspect(profile);
            var passed = !inspectionResult.IsAbnormal;
            var now = DateTimeOffset.Now;
            var recheck = new FaultRecheckRecord
            {
                TriggeredAt = now,
                TriggeredBy = NormalizeOperator(operatorName),
                InspectionTime = inspectionResult.InspectionTime,
                Outcome = passed ? FaultRecheckOutcomes.Passed : FaultRecheckOutcomes.Failed,
                OnlineStatus = inspectionResult.OnlineStatus,
                PlaybackHealthGrade = inspectionResult.PlaybackHealthGrade,
                PreferredProtocol = inspectionResult.PreferredProtocol,
                FailureReason = inspectionResult.FailureReason,
                Suggestion = inspectionResult.Suggestion,
                RelatedPlaybackReviewSessionId = record.RelatedPlaybackReviewSessionId,
                RelatedScreenshotSampleId = record.RelatedScreenshotSampleId
            };

            var updatedStatus = FaultClosureStateMachine.ResolveAfterRecheck(passed);
            var updated = BuildRecordWithRecheck(
                record,
                recheck,
                updatedStatus,
                NormalizeOperator(operatorName),
                now,
                passed
                    ? "基础巡检通过，记录进入复检通过待销警。"
                    : NormalizeNote(inspectionResult.FailureReason, "基础巡检仍未恢复，继续保留在待复检队列。"));

            ReplaceRecord(records, updated);
            SaveRecords(records);
            UpdateAiWorkflow(updated);
            return updated;
        }
    }

    public FaultClosureRecord ClearRecovered(string recordId, string operatorName, string noteText)
    {
        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var record = FindRecord(records, recordId);
            if (!record.CanClear)
            {
                throw new InvalidOperationException("当前记录尚未达到待销警状态。");
            }

            var updated = BuildRecordWithClear(
                record,
                FaultClosureStatuses.Cleared,
                FaultClearActionTypes.ClearAlarm,
                NormalizeOperator(operatorName),
                DateTimeOffset.Now,
                NormalizeNote(noteText, "人工确认恢复，已在本地完成销警闭环。"));

            ReplaceRecord(records, updated);
            SaveRecords(records);
            UpdateAiWorkflow(updated);
            return updated;
        }
    }

    public FaultClosureRecord CloseRecord(string recordId, string operatorName, string noteText)
    {
        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var record = FindRecord(records, recordId);
            if (!record.CanClose)
            {
                throw new InvalidOperationException("当前记录已处于终态，无需重复关闭。");
            }

            var updated = BuildRecordWithClear(
                record,
                FaultClosureStatuses.Closed,
                FaultClearActionTypes.Close,
                NormalizeOperator(operatorName),
                DateTimeOffset.Now,
                NormalizeNote(noteText, "本地闭环记录已手动关闭。"));

            ReplaceRecord(records, updated);
            SaveRecords(records);
            UpdateAiWorkflow(updated);
            return updated;
        }
    }

    public FaultClosureRecord CloseAsFalsePositive(string recordId, string operatorName, string noteText)
    {
        lock (_syncRoot)
        {
            var records = LoadAndSynchronize();
            var record = FindRecord(records, recordId);
            if (!record.CanClose)
            {
                throw new InvalidOperationException("当前记录已处于终态，无需重复处理。");
            }

            var updated = BuildRecordWithClear(
                record,
                FaultClosureStatuses.FalsePositiveClosed,
                FaultClearActionTypes.FalsePositiveClose,
                NormalizeOperator(operatorName),
                DateTimeOffset.Now,
                NormalizeNote(noteText, "人工确认误报，本地闭环直接关闭。"));

            ReplaceRecord(records, updated);
            SaveRecords(records);
            UpdateAiWorkflow(updated);
            return updated;
        }
    }

    private List<FaultClosureRecord> LoadAndSynchronize()
    {
        var records = _store.Load().ToList();
        var context = BuildManualReviewContext();
        var changed = false;

        foreach (var review in context.ManualReviews)
        {
            if (review.IsPending)
            {
                continue;
            }

            changed |= TrySynchronizeManualReview(records, review, context);
        }

        if (changed)
        {
            SaveRecords(records);
        }

        return records;
    }

    private ManualReviewContext BuildManualReviewContext()
    {
        var scopeLookup = _inspectionScopeService.GetCurrentScope().Devices
            .Where(item => !string.IsNullOrWhiteSpace(item.Device.DeviceCode))
            .GroupBy(item => item.Device.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var screenshotLookup = _screenshotSampleStore.Load()
            .Where(item => !string.IsNullOrWhiteSpace(item.SampleId))
            .GroupBy(item => item.SampleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var playbackLookup = _playbackReviewStore.Load()
            .Where(item => !string.IsNullOrWhiteSpace(item.SessionId))
            .GroupBy(item => item.SessionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return new ManualReviewContext(
            _manualReviewStore.Load()
                .OrderBy(item => item.ReviewedAt)
                .ToList(),
            screenshotLookup,
            playbackLookup,
            scopeLookup);
    }

    private bool TrySynchronizeManualReview(
        IList<FaultClosureRecord> records,
        ManualReviewRecord review,
        ManualReviewContext context)
    {
        var existing = FindMatchingRecord(records, review, context);
        if (existing is not null &&
            existing.LastManualReviewedAt.HasValue &&
            existing.LastManualReviewedAt.Value >= review.ReviewedAt)
        {
            return false;
        }

        UpsertManualReviewCore(records, review, context, forceApply: true);
        return true;
    }

    private FaultClosureRecord UpsertManualReviewCore(
        IList<FaultClosureRecord> records,
        ManualReviewRecord review,
        ManualReviewContext context,
        bool forceApply)
    {
        var existing = FindMatchingRecord(records, review, context);
        if (!forceApply &&
            existing is not null &&
            existing.LastManualReviewedAt.HasValue &&
            existing.LastManualReviewedAt.Value >= review.ReviewedAt)
        {
            return existing;
        }

        var screenshot = ResolveScreenshot(review, context.Screenshots);
        var playback = ResolvePlayback(review, screenshot, context.PlaybackResults);
        var aiAlert = ResolveAiAlert(review);
        var scopeDevice = ResolveScopeDevice(review, context.ScopeDevices);
        var sourceType = ResolveSourceType(review, aiAlert);
        var faultType = ResolveFaultType(review, aiAlert, playback);
        var faultSummary = ResolveFaultSummary(review, aiAlert, playback, scopeDevice);
        var primaryEvidenceType = ResolvePrimaryEvidenceType(review, aiAlert, screenshot);
        var primaryEvidenceImagePath = ResolvePrimaryEvidenceImage(aiAlert, screenshot);
        var primaryEvidenceCapturedAt = ResolvePrimaryEvidenceTime(aiAlert, screenshot);
        var directoryPath = ResolveDirectoryPath(scopeDevice);
        var requiresDispatch = review.RequiresDispatch || IsAbnormalConclusion(review.Conclusion);
        var requiresRecheck = review.RequiresRecheck || aiAlert is not null;
        var currentStatus = FaultClosureStateMachine.ResolveInitialStatus(review);

        var updated = new FaultClosureRecord
        {
            RecordId = existing?.RecordId ?? Guid.NewGuid().ToString("N"),
            TicketNumber = existing?.TicketNumber ?? BuildTicketNumber(review.ReviewedAt),
            DeviceCode = review.DeviceCode,
            DeviceName = string.IsNullOrWhiteSpace(review.DeviceName) ? review.DeviceCode : review.DeviceName,
            DirectoryPath = directoryPath,
            SchemeId = review.SchemeId,
            SchemeName = review.SchemeName,
            SourceType = sourceType,
            FaultType = faultType,
            FaultSummary = faultSummary,
            ReviewConclusion = review.Conclusion,
            CurrentStatus = currentStatus,
            RequiresDispatch = requiresDispatch,
            RequiresRecheck = requiresRecheck,
            IsFocusedPoint = scopeDevice?.IsFocused ?? existing?.IsFocusedPoint == true,
            RelatedEvidenceId = review.EvidenceId,
            RelatedManualReviewId = review.ReviewId,
            LastManualReviewedAt = review.ReviewedAt,
            RelatedScreenshotSampleId = FirstNonEmpty(review.RelatedScreenshotSampleId, screenshot?.SampleId),
            RelatedPlaybackReviewSessionId = FirstNonEmpty(review.RelatedPlaybackReviewSessionId, playback?.SessionId),
            RelatedAiAlertId = FirstNonEmpty(review.RelatedAiAlertId, aiAlert?.Id),
            RelatedDeviceCode = FirstNonEmpty(review.RelatedDeviceCode, review.DeviceCode),
            PrimaryEvidenceType = primaryEvidenceType,
            PrimaryEvidenceCapturedAt = primaryEvidenceCapturedAt,
            PrimaryEvidenceImagePath = primaryEvidenceImagePath,
            AiAlertSummary = aiAlert?.Summary ?? existing?.AiAlertSummary ?? string.Empty,
            PlaybackFileName = FirstNonEmpty(playback?.PlaybackFileName, screenshot?.PlaybackFileName, existing?.PlaybackFileName),
            CreatedAt = existing?.CreatedAt ?? review.ReviewedAt,
            CreatedBy = existing?.CreatedBy ?? review.Reviewer,
            UpdatedAt = review.ReviewedAt,
            UpdatedBy = review.Reviewer,
            LastActionNote = review.RemarkText,
            DispatchDraft = BuildDispatchDraft(existing, review, faultType, faultSummary, currentStatus),
            RecheckRecords = existing?.RecheckRecords ?? Array.Empty<FaultRecheckRecord>(),
            ClearRecords = existing?.ClearRecords ?? Array.Empty<FaultClearRecord>()
        };

        ReplaceRecord(records, updated);
        return updated;
    }

    private FaultClosureRecord BuildRecordWithDispatch(
        FaultClosureRecord current,
        string status,
        string operatorName,
        DateTimeOffset updatedAt,
        string noteText)
    {
        return new FaultClosureRecord
        {
            RecordId = current.RecordId,
            TicketNumber = current.TicketNumber,
            DeviceCode = current.DeviceCode,
            DeviceName = current.DeviceName,
            DirectoryPath = current.DirectoryPath,
            SchemeId = current.SchemeId,
            SchemeName = current.SchemeName,
            SourceType = current.SourceType,
            FaultType = current.FaultType,
            FaultSummary = current.FaultSummary,
            ReviewConclusion = current.ReviewConclusion,
            CurrentStatus = status,
            RequiresDispatch = current.RequiresDispatch,
            RequiresRecheck = current.RequiresRecheck,
            IsFocusedPoint = current.IsFocusedPoint,
            RelatedEvidenceId = current.RelatedEvidenceId,
            RelatedManualReviewId = current.RelatedManualReviewId,
            LastManualReviewedAt = current.LastManualReviewedAt,
            RelatedScreenshotSampleId = current.RelatedScreenshotSampleId,
            RelatedPlaybackReviewSessionId = current.RelatedPlaybackReviewSessionId,
            RelatedAiAlertId = current.RelatedAiAlertId,
            RelatedDeviceCode = current.RelatedDeviceCode,
            PrimaryEvidenceType = current.PrimaryEvidenceType,
            PrimaryEvidenceCapturedAt = current.PrimaryEvidenceCapturedAt,
            PrimaryEvidenceImagePath = current.PrimaryEvidenceImagePath,
            AiAlertSummary = current.AiAlertSummary,
            PlaybackFileName = current.PlaybackFileName,
            CreatedAt = current.CreatedAt,
            CreatedBy = current.CreatedBy,
            UpdatedAt = updatedAt,
            UpdatedBy = operatorName,
            LastActionNote = noteText,
            DispatchDraft = new FaultDispatchDraft
            {
                TicketNumber = current.DispatchDraft.TicketNumber,
                SiteName = current.DispatchDraft.SiteName,
                DeviceCode = current.DispatchDraft.DeviceCode,
                FaultType = current.DispatchDraft.FaultType,
                FaultSummary = current.DispatchDraft.FaultSummary,
                ManualReviewConclusion = current.DispatchDraft.ManualReviewConclusion,
                EvidenceSummary = current.DispatchDraft.EvidenceSummary,
                CreatedAt = current.DispatchDraft.CreatedAt,
                CreatedBy = current.DispatchDraft.CreatedBy,
                CurrentStatus = status,
                DispatchedAt = updatedAt,
                DispatchedBy = operatorName,
                ReservedChannels = current.DispatchDraft.ReservedChannels
            },
            RecheckRecords = current.RecheckRecords,
            ClearRecords = current.ClearRecords
        };
    }

    private FaultClosureRecord BuildRecordWithRecheck(
        FaultClosureRecord current,
        FaultRecheckRecord recheck,
        string status,
        string operatorName,
        DateTimeOffset updatedAt,
        string noteText)
    {
        var rechecks = current.RecheckRecords.ToList();
        rechecks.Add(recheck);

        return new FaultClosureRecord
        {
            RecordId = current.RecordId,
            TicketNumber = current.TicketNumber,
            DeviceCode = current.DeviceCode,
            DeviceName = current.DeviceName,
            DirectoryPath = current.DirectoryPath,
            SchemeId = current.SchemeId,
            SchemeName = current.SchemeName,
            SourceType = current.SourceType,
            FaultType = current.FaultType,
            FaultSummary = current.FaultSummary,
            ReviewConclusion = current.ReviewConclusion,
            CurrentStatus = status,
            RequiresDispatch = current.RequiresDispatch,
            RequiresRecheck = current.RequiresRecheck,
            IsFocusedPoint = current.IsFocusedPoint,
            RelatedEvidenceId = current.RelatedEvidenceId,
            RelatedManualReviewId = current.RelatedManualReviewId,
            LastManualReviewedAt = current.LastManualReviewedAt,
            RelatedScreenshotSampleId = current.RelatedScreenshotSampleId,
            RelatedPlaybackReviewSessionId = current.RelatedPlaybackReviewSessionId,
            RelatedAiAlertId = current.RelatedAiAlertId,
            RelatedDeviceCode = current.RelatedDeviceCode,
            PrimaryEvidenceType = current.PrimaryEvidenceType,
            PrimaryEvidenceCapturedAt = current.PrimaryEvidenceCapturedAt,
            PrimaryEvidenceImagePath = current.PrimaryEvidenceImagePath,
            AiAlertSummary = current.AiAlertSummary,
            PlaybackFileName = current.PlaybackFileName,
            CreatedAt = current.CreatedAt,
            CreatedBy = current.CreatedBy,
            UpdatedAt = updatedAt,
            UpdatedBy = operatorName,
            LastActionNote = noteText,
            DispatchDraft = new FaultDispatchDraft
            {
                TicketNumber = current.DispatchDraft.TicketNumber,
                SiteName = current.DispatchDraft.SiteName,
                DeviceCode = current.DispatchDraft.DeviceCode,
                FaultType = current.DispatchDraft.FaultType,
                FaultSummary = current.DispatchDraft.FaultSummary,
                ManualReviewConclusion = current.DispatchDraft.ManualReviewConclusion,
                EvidenceSummary = current.DispatchDraft.EvidenceSummary,
                CreatedAt = current.DispatchDraft.CreatedAt,
                CreatedBy = current.DispatchDraft.CreatedBy,
                CurrentStatus = status,
                DispatchedAt = current.DispatchDraft.DispatchedAt,
                DispatchedBy = current.DispatchDraft.DispatchedBy,
                ReservedChannels = current.DispatchDraft.ReservedChannels
            },
            RecheckRecords = rechecks,
            ClearRecords = current.ClearRecords
        };
    }

    private FaultClosureRecord BuildRecordWithClear(
        FaultClosureRecord current,
        string status,
        string clearAction,
        string operatorName,
        DateTimeOffset updatedAt,
        string noteText)
    {
        var clears = current.ClearRecords.ToList();
        clears.Add(new FaultClearRecord
        {
            ActionType = clearAction,
            PerformedAt = updatedAt,
            PerformedBy = operatorName,
            StatusBefore = current.CurrentStatus,
            StatusAfter = status,
            NoteText = noteText
        });

        return new FaultClosureRecord
        {
            RecordId = current.RecordId,
            TicketNumber = current.TicketNumber,
            DeviceCode = current.DeviceCode,
            DeviceName = current.DeviceName,
            DirectoryPath = current.DirectoryPath,
            SchemeId = current.SchemeId,
            SchemeName = current.SchemeName,
            SourceType = current.SourceType,
            FaultType = current.FaultType,
            FaultSummary = current.FaultSummary,
            ReviewConclusion = current.ReviewConclusion,
            CurrentStatus = status,
            RequiresDispatch = current.RequiresDispatch,
            RequiresRecheck = current.RequiresRecheck,
            IsFocusedPoint = current.IsFocusedPoint,
            RelatedEvidenceId = current.RelatedEvidenceId,
            RelatedManualReviewId = current.RelatedManualReviewId,
            LastManualReviewedAt = current.LastManualReviewedAt,
            RelatedScreenshotSampleId = current.RelatedScreenshotSampleId,
            RelatedPlaybackReviewSessionId = current.RelatedPlaybackReviewSessionId,
            RelatedAiAlertId = current.RelatedAiAlertId,
            RelatedDeviceCode = current.RelatedDeviceCode,
            PrimaryEvidenceType = current.PrimaryEvidenceType,
            PrimaryEvidenceCapturedAt = current.PrimaryEvidenceCapturedAt,
            PrimaryEvidenceImagePath = current.PrimaryEvidenceImagePath,
            AiAlertSummary = current.AiAlertSummary,
            PlaybackFileName = current.PlaybackFileName,
            CreatedAt = current.CreatedAt,
            CreatedBy = current.CreatedBy,
            UpdatedAt = updatedAt,
            UpdatedBy = operatorName,
            LastActionNote = noteText,
            DispatchDraft = new FaultDispatchDraft
            {
                TicketNumber = current.DispatchDraft.TicketNumber,
                SiteName = current.DispatchDraft.SiteName,
                DeviceCode = current.DispatchDraft.DeviceCode,
                FaultType = current.DispatchDraft.FaultType,
                FaultSummary = current.DispatchDraft.FaultSummary,
                ManualReviewConclusion = current.DispatchDraft.ManualReviewConclusion,
                EvidenceSummary = current.DispatchDraft.EvidenceSummary,
                CreatedAt = current.DispatchDraft.CreatedAt,
                CreatedBy = current.DispatchDraft.CreatedBy,
                CurrentStatus = status,
                DispatchedAt = current.DispatchDraft.DispatchedAt,
                DispatchedBy = current.DispatchDraft.DispatchedBy,
                ReservedChannels = current.DispatchDraft.ReservedChannels
            },
            RecheckRecords = current.RecheckRecords,
            ClearRecords = clears
        };
    }

    private static IEnumerable<FaultClosureRecord> ApplyFilters(
        IEnumerable<FaultClosureRecord> records,
        FaultClosureQuery query)
    {
        var filtered = records;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(item => string.Equals(item.CurrentStatus, query.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.FaultType))
        {
            filtered = filtered.Where(item => string.Equals(item.FaultType, query.FaultType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            filtered = filtered.Where(item => string.Equals(item.SourceType, query.SourceType, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PendingRecheckOnly)
        {
            filtered = filtered.Where(item => item.IsAwaitingRecheck);
        }

        if (query.FocusedOnly)
        {
            filtered = filtered.Where(item => item.IsFocusedPoint);
        }

        return filtered;
    }

    private static void ReplaceRecord(IList<FaultClosureRecord> records, FaultClosureRecord updated)
    {
        for (var index = 0; index < records.Count; index += 1)
        {
            if (!TextComparer.Equals(records[index].RecordId, updated.RecordId))
            {
                continue;
            }

            records[index] = updated;
            return;
        }

        records.Add(updated);
    }

    private static FaultClosureRecord FindRecord(IEnumerable<FaultClosureRecord> records, string recordId)
    {
        var record = records.FirstOrDefault(item => TextComparer.Equals(item.RecordId, recordId));
        return record ?? throw new InvalidOperationException("未找到故障闭环记录。");
    }

    private FaultClosureRecord? FindMatchingRecord(
        IEnumerable<FaultClosureRecord> records,
        ManualReviewRecord review,
        ManualReviewContext context)
    {
        var screenshot = ResolveScreenshot(review, context.Screenshots);
        var playback = ResolvePlayback(review, screenshot, context.PlaybackResults);
        var aiAlert = ResolveAiAlert(review);
        var sourceType = ResolveSourceType(review, aiAlert);
        var faultType = ResolveFaultType(review, aiAlert, playback);

        var byEvidence = records.FirstOrDefault(item => TextComparer.Equals(item.RelatedEvidenceId, review.EvidenceId));
        if (byEvidence is not null)
        {
            return byEvidence;
        }

        return records
            .Where(item =>
                !item.IsTerminal &&
                TextComparer.Equals(item.DeviceCode, review.DeviceCode) &&
                TextComparer.Equals(item.SourceType, sourceType) &&
                TextComparer.Equals(item.FaultType, faultType))
            .OrderByDescending(item => item.LastManualReviewedAt ?? item.UpdatedAt)
            .FirstOrDefault();
    }

    private DevicePointProfile ResolveDeviceProfile(FaultClosureRecord record)
    {
        var scopeDevice = _inspectionScopeService.GetCurrentScope().Devices
            .FirstOrDefault(item => TextComparer.Equals(item.Device.DeviceCode, record.DeviceCode));

        return _deviceCatalogService.GetDeviceProfile(record.DeviceCode, scopeDevice?.Device);
    }

    private static ScreenshotSampleResult? ResolveScreenshot(
        ManualReviewRecord review,
        IReadOnlyDictionary<string, ScreenshotSampleResult> screenshots)
    {
        if (!string.IsNullOrWhiteSpace(review.RelatedScreenshotSampleId) &&
            screenshots.TryGetValue(review.RelatedScreenshotSampleId, out var screenshot))
        {
            return screenshot;
        }

        if (!string.IsNullOrWhiteSpace(review.EvidenceId) &&
            screenshots.TryGetValue(review.EvidenceId, out screenshot))
        {
            return screenshot;
        }

        return null;
    }

    private static PlaybackReviewResult? ResolvePlayback(
        ManualReviewRecord review,
        ScreenshotSampleResult? screenshot,
        IReadOnlyDictionary<string, PlaybackReviewResult> playbackResults)
    {
        if (!string.IsNullOrWhiteSpace(review.RelatedPlaybackReviewSessionId) &&
            playbackResults.TryGetValue(review.RelatedPlaybackReviewSessionId, out var playback))
        {
            return playback;
        }

        if (screenshot is not null &&
            !string.IsNullOrWhiteSpace(screenshot.ReviewSessionId) &&
            playbackResults.TryGetValue(screenshot.ReviewSessionId, out playback))
        {
            return playback;
        }

        return null;
    }

    private AiAlertDetail? ResolveAiAlert(ManualReviewRecord review)
    {
        return string.IsNullOrWhiteSpace(review.RelatedAiAlertId)
            ? null
            : _aiAlertService.GetDetail(review.RelatedAiAlertId);
    }

    private static InspectionScopeDevice? ResolveScopeDevice(
        ManualReviewRecord review,
        IReadOnlyDictionary<string, InspectionScopeDevice> scopeDevices)
    {
        if (!string.IsNullOrWhiteSpace(review.DeviceCode) &&
            scopeDevices.TryGetValue(review.DeviceCode, out var scopeDevice))
        {
            return scopeDevice;
        }

        if (!string.IsNullOrWhiteSpace(review.RelatedDeviceCode) &&
            scopeDevices.TryGetValue(review.RelatedDeviceCode, out scopeDevice))
        {
            return scopeDevice;
        }

        return null;
    }

    private static string ResolveSourceType(ManualReviewRecord review, AiAlertDetail? aiAlert)
    {
        if (aiAlert is not null || !string.IsNullOrWhiteSpace(review.RelatedAiAlertId))
        {
            return FaultClosureSourceTypes.AiAlert;
        }

        if (string.Equals(review.SourceKind, ManualReviewSourceKinds.Playback, StringComparison.OrdinalIgnoreCase))
        {
            return FaultClosureSourceTypes.PlaybackReview;
        }

        if (string.Equals(review.SourceKind, ManualReviewSourceKinds.Live, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(review.RelatedScreenshotSampleId))
        {
            return FaultClosureSourceTypes.LiveReview;
        }

        return FaultClosureSourceTypes.InspectionFailure;
    }

    private static string ResolveFaultType(
        ManualReviewRecord review,
        AiAlertDetail? aiAlert,
        PlaybackReviewResult? playback)
    {
        if (IsAbnormalConclusion(review.Conclusion))
        {
            return ManualReviewTextMapper.ToConclusionText(review.Conclusion);
        }

        if (string.Equals(review.Conclusion, ManualReviewConclusions.FalsePositive, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(aiAlert?.AlertTypeName, playback?.ReviewTargetText, "误报");
        }

        return FirstNonEmpty(aiAlert?.AlertTypeName, "恢复确认");
    }

    private static string ResolveFaultSummary(
        ManualReviewRecord review,
        AiAlertDetail? aiAlert,
        PlaybackReviewResult? playback,
        InspectionScopeDevice? scopeDevice)
    {
        if (!string.IsNullOrWhiteSpace(review.RemarkText))
        {
            return review.RemarkText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(aiAlert?.Summary))
        {
            return aiAlert.Summary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(playback?.FailureReason))
        {
            return playback.FailureReason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(scopeDevice?.LatestInspection?.FailureReason))
        {
            return scopeDevice.LatestInspection.FailureReason.Trim();
        }

        return $"{ManualReviewTextMapper.ToConclusionText(review.Conclusion)} / {ManualReviewTextMapper.ToSourceText(review.SourceKind)}";
    }

    private static string ResolvePrimaryEvidenceType(
        ManualReviewRecord review,
        AiAlertDetail? aiAlert,
        ScreenshotSampleResult? screenshot)
    {
        if (aiAlert is not null || !string.IsNullOrWhiteSpace(review.RelatedAiAlertId))
        {
            return FaultClosureEvidenceTypes.AiSnapshot;
        }

        if (screenshot is not null)
        {
            return FaultClosureEvidenceTypes.Screenshot;
        }

        return FaultClosureEvidenceTypes.InspectionResult;
    }

    private static string ResolvePrimaryEvidenceImage(AiAlertDetail? aiAlert, ScreenshotSampleResult? screenshot)
    {
        return FirstNonEmpty(
            screenshot?.ImagePath,
            aiAlert?.SnapshotImageUrl,
            aiAlert?.ThumbnailImageUrl,
            aiAlert?.BackgroundImageUrl,
            aiAlert?.CloudFileIconUrl);
    }

    private static DateTimeOffset? ResolvePrimaryEvidenceTime(AiAlertDetail? aiAlert, ScreenshotSampleResult? screenshot)
    {
        return screenshot?.CapturedAt ?? aiAlert?.CreateTime;
    }

    private static string ResolveDirectoryPath(InspectionScopeDevice? scopeDevice)
    {
        return FirstNonEmpty(
            scopeDevice?.Device.DirectoryPath,
            scopeDevice?.Device.DirectoryName);
    }

    private static FaultDispatchDraft BuildDispatchDraft(
        FaultClosureRecord? existing,
        ManualReviewRecord review,
        string faultType,
        string faultSummary,
        string currentStatus)
    {
        var ticketNumber = existing?.DispatchDraft.TicketNumber
            ?? existing?.TicketNumber
            ?? BuildTicketNumber(review.ReviewedAt);

        return new FaultDispatchDraft
        {
            TicketNumber = ticketNumber,
            SiteName = string.IsNullOrWhiteSpace(review.DeviceName) ? review.DeviceCode : review.DeviceName,
            DeviceCode = review.DeviceCode,
            FaultType = faultType,
            FaultSummary = faultSummary,
            ManualReviewConclusion = review.Conclusion,
            EvidenceSummary = string.Join(" / ", new[]
            {
                string.IsNullOrWhiteSpace(review.RelatedScreenshotSampleId) ? null : "截图",
                string.IsNullOrWhiteSpace(review.RelatedPlaybackReviewSessionId) ? null : "回看会话",
                string.IsNullOrWhiteSpace(review.RelatedAiAlertId) ? null : "AI告警",
                string.IsNullOrWhiteSpace(review.RelatedDeviceCode) ? null : "当前点位"
            }.Where(item => !string.IsNullOrWhiteSpace(item))),
            CreatedAt = existing?.DispatchDraft.CreatedAt ?? review.ReviewedAt,
            CreatedBy = existing?.DispatchDraft.CreatedBy ?? review.Reviewer,
            CurrentStatus = currentStatus,
            DispatchedAt = existing?.DispatchDraft.DispatchedAt,
            DispatchedBy = existing?.DispatchDraft.DispatchedBy ?? string.Empty,
            ReservedChannels = existing?.DispatchDraft.ReservedChannels ??
            [
                "EnterpriseWeComWebhook",
                "QuantumSecretWebhook"
            ]
        };
    }

    private static string BuildTicketNumber(DateTimeOffset reviewedAt)
    {
        return $"WO-{reviewedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..27];
    }

    private void UpdateAiWorkflow(FaultClosureRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.RelatedAiAlertId))
        {
            return;
        }

        var workflowStatus = record.CurrentStatus switch
        {
            FaultClosureStatuses.FalsePositiveClosed => AiAlertWorkflowStatus.Ignored,
            FaultClosureStatuses.Cleared => AiAlertWorkflowStatus.Recovered,
            FaultClosureStatuses.Closed => AiAlertWorkflowStatus.Recovered,
            FaultClosureStatuses.DispatchedPendingProcess => AiAlertWorkflowStatus.Dispatched,
            _ => AiAlertWorkflowStatus.Confirmed
        };

        _aiAlertService.UpdateWorkflowStatus(record.RelatedAiAlertId, workflowStatus, record.LastActionNote);
    }

    private void SaveRecords(IReadOnlyList<FaultClosureRecord> records)
    {
        _store.Save(records
            .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.UpdatedAt)
            .ToList());
    }

    private static bool IsAbnormalConclusion(string? conclusion)
    {
        return !string.IsNullOrWhiteSpace(conclusion) &&
               !string.Equals(conclusion, ManualReviewConclusions.Pending, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(conclusion, ManualReviewConclusions.Normal, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(conclusion, ManualReviewConclusions.FalsePositive, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeOperator(string? operatorName)
    {
        if (!string.IsNullOrWhiteSpace(operatorName))
        {
            return operatorName.Trim();
        }

        return string.IsNullOrWhiteSpace(Environment.UserName)
            ? "current-operator"
            : Environment.UserName.Trim();
    }

    private static string NormalizeNote(string? noteText, string fallback)
    {
        return string.IsNullOrWhiteSpace(noteText) ? fallback : noteText.Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private sealed record ManualReviewContext(
        IReadOnlyList<ManualReviewRecord> ManualReviews,
        IReadOnlyDictionary<string, ScreenshotSampleResult> Screenshots,
        IReadOnlyDictionary<string, PlaybackReviewResult> PlaybackResults,
        IReadOnlyDictionary<string, InspectionScopeDevice> ScopeDevices);
}
