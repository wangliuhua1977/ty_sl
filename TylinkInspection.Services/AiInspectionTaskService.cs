using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed partial class AiInspectionTaskService : IAiInspectionTaskService
{
    private static readonly byte[] PlaceholderPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9WnWlO8AAAAASUVORK5CYII=");

    private readonly IAiInspectionTaskStore _taskStore;
    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly IPlaybackReviewService _playbackReviewService;
    private readonly IScreenshotSamplingService _screenshotSamplingService;
    private readonly IFaultClosureService _faultClosureService;
    private readonly IRecheckSchedulerService _recheckSchedulerService;
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _queueSignal = new(0);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private List<AiInspectionTaskBatch> _batches = [];
    private bool _started;

    public AiInspectionTaskService(
        IAiInspectionTaskStore taskStore,
        IInspectionScopeService inspectionScopeService,
        IDeviceCatalogService deviceCatalogService,
        IDeviceInspectionService deviceInspectionService,
        IPlaybackReviewService playbackReviewService,
        IScreenshotSamplingService screenshotSamplingService,
        IFaultClosureService faultClosureService,
        IRecheckSchedulerService recheckSchedulerService)
    {
        _taskStore = taskStore;
        _inspectionScopeService = inspectionScopeService;
        _deviceCatalogService = deviceCatalogService;
        _deviceInspectionService = deviceInspectionService;
        _playbackReviewService = playbackReviewService;
        _screenshotSamplingService = screenshotSamplingService;
        _faultClosureService = faultClosureService;
        _recheckSchedulerService = recheckSchedulerService;
    }

    public event EventHandler? TasksChanged;

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _batches = _taskStore.LoadAll()
                .OrderByDescending(item => item.CreatedAt)
                .ToList();
            _batches = RecoverBatches(_batches);
            PersistNoLock();
            _loopCts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token));

            if (HasPendingWorkNoLock())
            {
                _queueSignal.Release();
            }
        }

        NotifyChanged();
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? loopTask;

        lock (_syncRoot)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            cts = _loopCts;
            loopTask = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }

        try
        {
            cts?.Cancel();
            loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public AiInspectionTaskOverview GetOverview(AiInspectionTaskQuery? query = null)
    {
        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            var filtered = ApplyQuery(_batches, query ?? new AiInspectionTaskQuery()).ToList();

            return new AiInspectionTaskOverview
            {
                TotalTaskCount = filtered.Count,
                PendingTaskCount = filtered.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase)),
                RunningTaskCount = filtered.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Running, StringComparison.OrdinalIgnoreCase)),
                SucceededTaskCount = filtered.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Succeeded, StringComparison.OrdinalIgnoreCase)),
                FailedTaskCount = filtered.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase)),
                PartiallyCompletedTaskCount = filtered.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase)),
                CanceledTaskCount = filtered.Count(item => string.Equals(item.Status, AiInspectionTaskStatus.Canceled, StringComparison.OrdinalIgnoreCase)),
                TotalItemCount = filtered.Sum(item => item.TotalCount),
                AbnormalItemCount = filtered.Sum(item => item.AbnormalCount),
                GeneratedAt = DateTimeOffset.Now,
                StatusMessage = filtered.Count == 0
                    ? "当前尚未创建 AI 智能巡检任务。"
                    : $"当前共管理 {filtered.Count} 个批次任务，累计 {filtered.Sum(item => item.TotalCount)} 个点位子任务。"
            };
        }
    }

    public IReadOnlyList<AiInspectionTaskBatch> Query(AiInspectionTaskQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            return ApplyQuery(_batches, query)
                .OrderBy(item => GetStatusOrder(item.Status))
                .ThenByDescending(item => item.CreatedAt)
                .ToList();
        }
    }

    public AiInspectionTaskBatch? GetDetail(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return null;
        }

        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            return _batches.FirstOrDefault(item => string.Equals(item.TaskId, taskId.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    public AiInspectionTaskBatch CreateTask(AiInspectionTaskCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = _inspectionScopeService.GetCurrentScope();
        var pendingRecheckLookup = BuildPendingRecheckLookup();
        var selectedDevices = ResolveScopeDevices(scope, request.ScopeMode, pendingRecheckLookup)
            .GroupBy(item => item.Device.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.IsFocused)
            .ThenBy(item => item.Device.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedDevices.Count == 0)
        {
            throw new InvalidOperationException("当前任务范围下没有可执行点位。");
        }

        var taskId = Guid.NewGuid().ToString("N");
        var createdBy = NormalizeOperator(request.CreatedBy);
        var createdAt = DateTimeOffset.Now;
        var normalizedTaskType = NormalizeTaskType(request.TaskType);
        var normalizedScopeMode = NormalizeScopeMode(request.ScopeMode);
        var taskName = string.IsNullOrWhiteSpace(request.TaskName)
            ? $"{scope.CurrentScheme.Name} / {AiInspectionTaskTextMapper.ToTaskTypeText(normalizedTaskType)} / {AiInspectionTaskTextMapper.ToScopeModeText(normalizedScopeMode)}"
            : request.TaskName.Trim();

        var items = selectedDevices
            .Select(device => new AiInspectionTaskItem
            {
                ItemId = Guid.NewGuid().ToString("N"),
                DeviceCode = device.Device.DeviceCode,
                DeviceName = device.Device.DeviceName,
                DirectoryPath = device.Device.DirectoryPath,
                IsFocusedPoint = device.IsFocused,
                IsPendingRecheckPoint = device.NeedRecheck || pendingRecheckLookup.ContainsKey(device.Device.DeviceCode),
                ExecutionStatus = AiInspectionTaskItemStatus.Pending
            })
            .ToList();

        var batch = new AiInspectionTaskBatch
        {
            TaskId = taskId,
            TaskName = taskName,
            SchemeId = scope.CurrentScheme.Id,
            SchemeName = scope.CurrentScheme.Name,
            TaskType = normalizedTaskType,
            ScopeMode = normalizedScopeMode,
            TotalCount = items.Count,
            Status = AiInspectionTaskStatus.Pending,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            LatestResultSummary = $"已生成 {items.Count} 个点位子任务，等待执行。",
            Items = items,
            ExecutionRecords =
            [
                CreateRecord(taskId, string.Empty, string.Empty, string.Empty, createdAt, $"批次已创建，覆盖 {items.Count} 个点位。", "TonePrimaryBrush")
            ]
        };

        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            _batches.Insert(0, batch);
            PersistNoLock();
        }

        NotifyChanged();

        if (request.ExecuteImmediately)
        {
            return StartTask(taskId, createdBy);
        }

        return batch;
    }

    public AiInspectionTaskBatch StartTask(string taskId, string? operatorName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        AiInspectionTaskBatch updated;
        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            var current = FindBatchNoLock(taskId);
            updated = PrepareBatchForStart(current, NormalizeOperator(operatorName));
            ReplaceBatchNoLock(updated);
            PersistNoLock();
            _queueSignal.Release();
        }

        NotifyChanged();
        return updated;
    }

    public AiInspectionTaskBatch CancelTask(string taskId, string? operatorName, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        AiInspectionTaskBatch updated;
        lock (_syncRoot)
        {
            EnsureStartedNoLock();
            var current = FindBatchNoLock(taskId);
            var now = DateTimeOffset.Now;
            var canceledItems = current.Items
                .Select(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase)
                    ? item with
                    {
                        ExecutionStatus = AiInspectionTaskItemStatus.Canceled,
                        LastResultSummary = "任务已取消。",
                        CompletedAt = now
                    }
                    : item)
                .ToList();

            updated = RebuildBatch(
                current,
                canceledItems,
                current.ExecutionRecords.Append(
                    CreateRecord(
                        current.TaskId,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        now,
                        $"批次已取消未完成子任务。{NormalizeNote(note, string.Empty)}".Trim(),
                        "ToneInfoBrush"))
                    .ToList(),
                current.StartedAt,
                DetermineCompletedAt(canceledItems, current.CompletedAt),
                normalizeLatestSummary: false,
                explicitLatestSummary: NormalizeNote(note, "已取消未完成子任务。"));

            ReplaceBatchNoLock(updated);
            PersistNoLock();
        }

        NotifyChanged();
        return updated;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _queueSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                ExecutionWorkItem? next;
                lock (_syncRoot)
                {
                    next = TryDequeueNextNoLock();
                }

                if (next is null)
                {
                    break;
                }

                NotifyChanged();
                ExecuteWorkItem(next.Value);
            }
        }
    }

    private ExecutionWorkItem? TryDequeueNextNoLock()
    {
        EnsureStartedNoLock();

        var candidateBatch = _batches
            .Where(item =>
                string.Equals(item.Status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, AiInspectionTaskStatus.Running, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, AiInspectionTaskStatus.PartiallyCompleted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, AiInspectionTaskStatus.Failed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.StartedAt ?? item.CreatedAt)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefault(item => item.Items.Any(device => string.Equals(device.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase)));

        if (candidateBatch is null)
        {
            return null;
        }

        var nextItem = candidateBatch.Items.First(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase));
        var now = DateTimeOffset.Now;
        var updatedItems = candidateBatch.Items
            .Select(item => item.ItemId == nextItem.ItemId
                ? item with
                {
                    ExecutionStatus = AiInspectionTaskItemStatus.Running,
                    StartedAt = now,
                    LastError = string.Empty,
                    LastResultSummary = "执行中..."
                }
                : item)
            .ToList();

        var updatedBatch = candidateBatch with
        {
            Status = AiInspectionTaskStatus.Running,
            StartedAt = candidateBatch.StartedAt ?? now,
            CompletedAt = null,
            Items = updatedItems,
            ExecutionRecords = candidateBatch.ExecutionRecords.Append(
                CreateRecord(candidateBatch.TaskId, nextItem.ItemId, nextItem.DeviceCode, nextItem.DeviceName, now, $"开始执行点位 {nextItem.DeviceName}。", "TonePrimaryBrush"))
                .ToList(),
            LatestResultSummary = $"正在执行 {nextItem.DeviceName}。"
        };

        ReplaceBatchNoLock(updatedBatch);
        PersistNoLock();

        return new ExecutionWorkItem(updatedBatch.TaskId, nextItem.ItemId);
    }

    private void ExecuteWorkItem(ExecutionWorkItem workItem)
    {
        TaskExecutionResult result;

        try
        {
            result = ExecuteItemCore(workItem);
        }
        catch (Exception ex)
        {
            result = new TaskExecutionResult(
                AiInspectionTaskItemStatus.Failed,
                ex.Message,
                NormalizeNote(ex.Message, "任务执行失败。"),
                true,
                null,
                null,
                null,
                "ToneDangerBrush");
        }

        lock (_syncRoot)
        {
            var currentBatch = FindBatchNoLock(workItem.TaskId);
            var currentItem = currentBatch.Items.First(item => item.ItemId == workItem.ItemId);
            var now = DateTimeOffset.Now;
            var updatedItems = currentBatch.Items
                .Select(item => item.ItemId == workItem.ItemId
                    ? item with
                    {
                        ExecutionStatus = result.ItemStatus,
                        LastError = result.LastError,
                        LastResultSummary = result.ResultSummary,
                        IsAbnormalResult = result.IsAbnormal,
                        LinkedInspectionResultId = result.LinkedInspectionResultId,
                        LinkedReviewId = result.LinkedReviewId,
                        LinkedClosureId = result.LinkedClosureId,
                        CompletedAt = now
                    }
                    : item)
                .ToList();

            var updatedBatch = RebuildBatch(
                currentBatch,
                updatedItems,
                currentBatch.ExecutionRecords.Append(
                    CreateRecord(
                        currentBatch.TaskId,
                        currentItem.ItemId,
                        currentItem.DeviceCode,
                        currentItem.DeviceName,
                        now,
                        result.ResultSummary,
                        result.AccentResourceKey))
                    .ToList(),
                currentBatch.StartedAt,
                DetermineCompletedAt(updatedItems, currentBatch.CompletedAt),
                normalizeLatestSummary: false,
                explicitLatestSummary: result.ResultSummary);

            ReplaceBatchNoLock(updatedBatch);
            PersistNoLock();
        }

        NotifyChanged();

        lock (_syncRoot)
        {
            if (HasPendingWorkNoLock())
            {
                _queueSignal.Release();
            }
        }
    }

    private TaskExecutionResult ExecuteItemCore(ExecutionWorkItem workItem)
    {
        var batch = GetDetail(workItem.TaskId) ?? throw new InvalidOperationException("批次任务不存在。");
        var item = batch.Items.First(device => device.ItemId == workItem.ItemId);

        return batch.TaskType switch
        {
            AiInspectionTaskType.BasicInspection => ExecuteBasicInspection(item),
            AiInspectionTaskType.PlaybackReview => ExecutePlaybackReview(item),
            AiInspectionTaskType.ScreenshotReviewPreparation => ExecuteScreenshotPreparation(item),
            AiInspectionTaskType.Recheck => ExecuteRecheck(item),
            _ => throw new InvalidOperationException("未知任务类型。")
        };
    }

    private TaskExecutionResult ExecuteBasicInspection(AiInspectionTaskItem item)
    {
        var profile = _deviceCatalogService.GetDeviceProfile(item.DeviceCode);
        var inspection = _deviceInspectionService.Inspect(profile);
        var summary = inspection.IsAbnormal
            ? $"基础巡检完成，结果异常，健康度 {inspection.PlaybackHealthGrade}。"
            : $"基础巡检完成，健康度 {inspection.PlaybackHealthGrade}，首选协议 {inspection.PreferredProtocolText}。";

        return new TaskExecutionResult(
            AiInspectionTaskItemStatus.Succeeded,
            string.Empty,
            summary,
            inspection.IsAbnormal,
            null,
            null,
            null,
            inspection.IsAbnormal ? "ToneWarningBrush" : "ToneSuccessBrush");
    }

    private TaskExecutionResult ExecutePlaybackReview(AiInspectionTaskItem item)
    {
        var profile = _deviceCatalogService.GetDeviceProfile(item.DeviceCode);
        var session = _playbackReviewService.PrepareLiveReview(new PlaybackReviewPreparationRequest
        {
            DeviceCode = profile.Device.DeviceCode,
            DeviceName = profile.Device.DeviceName,
            NetTypeCode = profile.Device.NetTypeCode,
            BaseInspectionResult = _deviceInspectionService.GetLatestResult(profile.Device.DeviceCode),
            ForceRefresh = true
        });

        if (!session.HasSources)
        {
            return new TaskExecutionResult(
                AiInspectionTaskItemStatus.Failed,
                NormalizeNote(session.DiagnosticMessage, "未生成可用播放复核地址。"),
                "播放复核准备失败，未生成可用地址。",
                true,
                null,
                session.SessionId,
                null,
                "ToneDangerBrush");
        }

        var review = _playbackReviewService.CompleteReview(new PlaybackReviewOutcome
        {
            SessionId = session.SessionId,
            ReviewTargetKind = session.ReviewTargetKind,
            DeviceCode = session.DeviceCode,
            DeviceName = session.DeviceName,
            ReviewedAt = DateTimeOffset.Now,
            PlaybackStarted = true,
            FirstFrameVisible = true,
            StartupDurationMs = 0,
            UsedProtocol = session.PreferredProtocol,
            UsedUrl = session.PreferredUrl,
            UsedFallback = false,
            FailureReason = string.Empty,
            VideoEncoding = session.VideoEncoding
        });

        return new TaskExecutionResult(
            AiInspectionTaskItemStatus.Succeeded,
            string.Empty,
            $"播放复核链路已准备完成，首选 {session.PreferredProtocol}，结果 {review.ReviewOutcomeText}。",
            false,
            null,
            session.SessionId,
            null,
            "ToneSuccessBrush");
    }

    private TaskExecutionResult ExecuteScreenshotPreparation(AiInspectionTaskItem item)
    {
        var profile = _deviceCatalogService.GetDeviceProfile(item.DeviceCode);
        var session = _playbackReviewService.PrepareLiveReview(new PlaybackReviewPreparationRequest
        {
            DeviceCode = profile.Device.DeviceCode,
            DeviceName = profile.Device.DeviceName,
            NetTypeCode = profile.Device.NetTypeCode,
            BaseInspectionResult = _deviceInspectionService.GetLatestResult(profile.Device.DeviceCode),
            ForceRefresh = true
        });

        if (!session.HasSources)
        {
            return new TaskExecutionResult(
                AiInspectionTaskItemStatus.Failed,
                NormalizeNote(session.DiagnosticMessage, "未生成截图预备地址。"),
                "截图复核预备失败，未生成可用播放源。",
                true,
                null,
                session.SessionId,
                null,
                "ToneDangerBrush");
        }

        var sample = _screenshotSamplingService.SaveSample(new ScreenshotSampleRequest
        {
            ReviewSessionId = session.SessionId,
            ReviewTargetKind = "Live",
            DeviceCode = item.DeviceCode,
            DeviceName = item.DeviceName,
            Protocol = session.PreferredProtocol,
            SourceUrl = session.PreferredUrl,
            CapturedAt = DateTimeOffset.Now,
            ImageBytes = PlaceholderPngBytes
        });

        return new TaskExecutionResult(
            AiInspectionTaskItemStatus.Succeeded,
            string.Empty,
            $"截图复核预备已完成，已沉淀样本 {sample.ImageFileName}。",
            false,
            null,
            session.SessionId,
            null,
            "ToneSuccessBrush");
    }

    private TaskExecutionResult ExecuteRecheck(AiInspectionTaskItem item)
    {
        var closure = BuildPendingRecheckLookup()
            .Values
            .Where(record => string.Equals(record.DeviceCode, item.DeviceCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.UpdatedAt)
            .FirstOrDefault();

        if (closure is null)
        {
            return new TaskExecutionResult(
                AiInspectionTaskItemStatus.Failed,
                "未找到待复检闭环记录。",
                "复检任务失败，未找到待复检闭环记录。",
                true,
                null,
                null,
                null,
                "ToneDangerBrush");
        }

        var recheckTask = _recheckSchedulerService.EnsureTask(closure, NormalizeOperator(null));
        var executed = _recheckSchedulerService.TriggerTaskNow(recheckTask.TaskId, NormalizeOperator(null));
        var isPassed = string.Equals(executed.LastExecutionOutcome, RecheckExecutionOutcomes.Passed, StringComparison.OrdinalIgnoreCase);

        return new TaskExecutionResult(
            isPassed ? AiInspectionTaskItemStatus.Succeeded : AiInspectionTaskItemStatus.Failed,
            isPassed ? string.Empty : NormalizeNote(executed.LastFailureReason, executed.LastExecutionSummary),
            NormalizeNote(executed.LastExecutionSummary, "复检任务已执行。"),
            !isPassed,
            null,
            executed.LastPlaybackReviewSessionId,
            closure.RecordId,
            isPassed ? "ToneSuccessBrush" : "ToneWarningBrush");
    }

    private static IEnumerable<AiInspectionTaskBatch> ApplyQuery(IEnumerable<AiInspectionTaskBatch> batches, AiInspectionTaskQuery query)
    {
        var filtered = batches;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            filtered = filtered.Where(item =>
                item.TaskName.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                item.SchemeName.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                item.Items.Any(device =>
                    device.DeviceCode.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                    device.DeviceName.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filtered = filtered.Where(item => string.Equals(item.Status, query.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TaskType))
        {
            filtered = filtered.Where(item => string.Equals(item.TaskType, query.TaskType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.ScopeMode))
        {
            filtered = filtered.Where(item => string.Equals(item.ScopeMode, query.ScopeMode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SchemeId))
        {
            filtered = filtered.Where(item => string.Equals(item.SchemeId, query.SchemeId, StringComparison.OrdinalIgnoreCase));
        }

        if (query.StartTime.HasValue)
        {
            filtered = filtered.Where(item => item.CreatedAt >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            filtered = filtered.Where(item => item.CreatedAt <= query.EndTime.Value);
        }

        return filtered;
    }

    private static List<AiInspectionTaskBatch> RecoverBatches(IEnumerable<AiInspectionTaskBatch> batches)
    {
        var recovered = new List<AiInspectionTaskBatch>();
        foreach (var batch in batches)
        {
            var items = batch.Items
                .Select(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Running, StringComparison.OrdinalIgnoreCase)
                    ? item with
                    {
                        ExecutionStatus = AiInspectionTaskItemStatus.Pending,
                        LastResultSummary = "应用重启后已恢复到待执行队列。",
                        CompletedAt = null
                    }
                    : item)
                .ToList();

            var needsRecovery = items.Any(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase)) &&
                                !string.Equals(batch.Status, AiInspectionTaskStatus.Succeeded, StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(batch.Status, AiInspectionTaskStatus.Canceled, StringComparison.OrdinalIgnoreCase);
            var records = batch.ExecutionRecords.ToList();
            if (needsRecovery)
            {
                records.Add(CreateRecord(batch.TaskId, string.Empty, string.Empty, string.Empty, DateTimeOffset.Now, "检测到未完成任务，已恢复到本地执行队列。", "ToneWarningBrush"));
            }

            recovered.Add(RebuildBatch(
                batch,
                items,
                records,
                batch.StartedAt,
                DetermineCompletedAt(items, batch.CompletedAt),
                normalizeLatestSummary: false,
                explicitLatestSummary: needsRecovery ? "检测到未完成任务，已恢复到本地执行队列。" : batch.LatestResultSummary));
        }

        return recovered;
    }

    private Dictionary<string, FaultClosureRecord> BuildPendingRecheckLookup()
    {
        return _faultClosureService.GetOverview(new FaultClosureQuery())
            .Records
            .Where(item => item.IsAwaitingRecheck)
            .GroupBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<InspectionScopeDevice> ResolveScopeDevices(
        InspectionScopeResult scope,
        string? scopeMode,
        IReadOnlyDictionary<string, FaultClosureRecord> pendingRecheckLookup)
    {
        var normalizedMode = NormalizeScopeMode(scopeMode);
        var devices = scope.Devices.Where(item => item.IsInCurrentScope);

        return normalizedMode switch
        {
            AiInspectionTaskScopeMode.AbnormalOnly => devices
                .Where(item => item.NeedRecheck || item.LatestInspection?.IsAbnormal == true)
                .ToList(),
            AiInspectionTaskScopeMode.FocusedOnly => devices
                .Where(item => item.IsFocused)
                .ToList(),
            AiInspectionTaskScopeMode.PendingRecheckOnly => devices
                .Where(item => item.NeedRecheck || pendingRecheckLookup.ContainsKey(item.Device.DeviceCode))
                .ToList(),
            _ => devices.ToList()
        };
    }

    private static AiInspectionTaskBatch PrepareBatchForStart(AiInspectionTaskBatch batch, string operatorName)
    {
        var now = DateTimeOffset.Now;
        var items = batch.Items.Select(item =>
        {
            if (string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase))
            {
                return item with
                {
                    ExecutionStatus = AiInspectionTaskItemStatus.Pending,
                    LastError = string.Empty,
                    LastResultSummary = "已重新加入执行队列。",
                    CompletedAt = null
                };
            }

            return item;
        }).ToList();

        return RebuildBatch(
            batch,
            items,
            batch.ExecutionRecords.Append(CreateRecord(batch.TaskId, string.Empty, string.Empty, string.Empty, now, $"批次已由 {operatorName} 加入执行队列。", "TonePrimaryBrush")).ToList(),
            batch.StartedAt,
            null,
            normalizeLatestSummary: false,
            explicitLatestSummary: "批次已加入执行队列。");
    }

    private static AiInspectionTaskBatch RebuildBatch(
        AiInspectionTaskBatch source,
        IReadOnlyList<AiInspectionTaskItem> items,
        IReadOnlyList<AiInspectionTaskExecutionRecord> records,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        bool normalizeLatestSummary,
        string? explicitLatestSummary)
    {
        var succeededCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Succeeded, StringComparison.OrdinalIgnoreCase));
        var failedCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase));
        var canceledCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase));
        var abnormalCount = items.Count(item => item.IsAbnormalResult);

        return source with
        {
            SucceededCount = succeededCount,
            FailedCount = failedCount,
            AbnormalCount = abnormalCount,
            CanceledCount = canceledCount,
            Status = ResolveBatchStatus(items),
            StartedAt = startedAt,
            CompletedAt = completedAt,
            FailureSummary = BuildFailureSummary(items),
            LatestResultSummary = normalizeLatestSummary
                ? NormalizeNote(explicitLatestSummary, source.LatestResultSummary)
                : explicitLatestSummary ?? source.LatestResultSummary,
            Items = items,
            ExecutionRecords = records
        };
    }

    private static string ResolveBatchStatus(IReadOnlyList<AiInspectionTaskItem> items)
    {
        if (items.Any(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Running, StringComparison.OrdinalIgnoreCase)))
        {
            return AiInspectionTaskStatus.Running;
        }

        var pendingCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase));
        if (pendingCount > 0)
        {
            var finishedCount = items.Count - pendingCount;
            return finishedCount > 0 ? AiInspectionTaskStatus.PartiallyCompleted : AiInspectionTaskStatus.Pending;
        }

        var succeededCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Succeeded, StringComparison.OrdinalIgnoreCase));
        var failedCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase));
        var canceledCount = items.Count(item => string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase));

        if (succeededCount == items.Count)
        {
            return AiInspectionTaskStatus.Succeeded;
        }

        if (failedCount == items.Count)
        {
            return AiInspectionTaskStatus.Failed;
        }

        if (canceledCount == items.Count)
        {
            return AiInspectionTaskStatus.Canceled;
        }

        return AiInspectionTaskStatus.PartiallyCompleted;
    }

    private static string BuildFailureSummary(IEnumerable<AiInspectionTaskItem> items)
    {
        var failures = items
            .Where(item =>
                string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Failed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(item =>
            {
                var reason = string.Equals(item.ExecutionStatus, AiInspectionTaskItemStatus.Canceled, StringComparison.OrdinalIgnoreCase)
                    ? "已取消"
                    : NormalizeNote(item.LastError, "执行失败");
                return $"{item.DeviceName}: {reason}";
            })
            .ToList();

        return failures.Count == 0 ? "暂无失败摘要。" : string.Join("；", failures);
    }

    private static DateTimeOffset? DetermineCompletedAt(IReadOnlyList<AiInspectionTaskItem> items, DateTimeOffset? fallback)
    {
        var status = ResolveBatchStatus(items);
        if (string.Equals(status, AiInspectionTaskStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, AiInspectionTaskStatus.Running, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var latestCompletedAt = items
            .Where(item => item.CompletedAt.HasValue)
            .Select(item => item.CompletedAt!.Value)
            .OrderByDescending(item => item)
            .FirstOrDefault();

        return latestCompletedAt == default ? fallback ?? DateTimeOffset.Now : latestCompletedAt;
    }

    private static AiInspectionTaskExecutionRecord CreateRecord(
        string taskId,
        string itemId,
        string deviceCode,
        string deviceName,
        DateTimeOffset timestamp,
        string message,
        string accentResourceKey)
    {
        return new AiInspectionTaskExecutionRecord
        {
            TaskId = taskId,
            ItemId = itemId,
            DeviceCode = deviceCode,
            DeviceName = deviceName,
            Timestamp = timestamp,
            Message = message,
            AccentResourceKey = accentResourceKey
        };
    }

    private static int GetStatusOrder(string status)
    {
        return status switch
        {
            AiInspectionTaskStatus.Running => 0,
            AiInspectionTaskStatus.Pending => 1,
            AiInspectionTaskStatus.PartiallyCompleted => 2,
            AiInspectionTaskStatus.Failed => 3,
            AiInspectionTaskStatus.Succeeded => 4,
            AiInspectionTaskStatus.Canceled => 5,
            _ => 9
        };
    }

    private void EnsureStartedNoLock()
    {
        if (!_started)
        {
            throw new InvalidOperationException("AI inspection task service has not been started.");
        }
    }

    private bool HasPendingWorkNoLock()
    {
        return _batches.Any(item => item.Items.Any(device => string.Equals(device.ExecutionStatus, AiInspectionTaskItemStatus.Pending, StringComparison.OrdinalIgnoreCase)));
    }

    private AiInspectionTaskBatch FindBatchNoLock(string taskId)
    {
        return _batches.FirstOrDefault(item => string.Equals(item.TaskId, taskId.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("未找到对应的 AI 巡检任务。");
    }

    private void ReplaceBatchNoLock(AiInspectionTaskBatch batch)
    {
        var index = _batches.FindIndex(item => string.Equals(item.TaskId, batch.TaskId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("未找到对应的 AI 巡检任务。");
        }

        _batches[index] = batch;
        _batches = _batches
            .OrderBy(item => GetStatusOrder(item.Status))
            .ThenByDescending(item => item.CreatedAt)
            .ToList();
    }

    private void PersistNoLock()
    {
        _taskStore.SaveAll(_batches);
    }

    private void NotifyChanged()
    {
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string NormalizeOperator(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Environment.UserName : value.Trim();
    }

    private static string NormalizeTaskType(string? taskType)
    {
        return taskType switch
        {
            AiInspectionTaskType.BasicInspection => AiInspectionTaskType.BasicInspection,
            AiInspectionTaskType.PlaybackReview => AiInspectionTaskType.PlaybackReview,
            AiInspectionTaskType.ScreenshotReviewPreparation => AiInspectionTaskType.ScreenshotReviewPreparation,
            AiInspectionTaskType.Recheck => AiInspectionTaskType.Recheck,
            _ => AiInspectionTaskType.BasicInspection
        };
    }

    private static string NormalizeScopeMode(string? scopeMode)
    {
        return scopeMode switch
        {
            AiInspectionTaskScopeMode.AbnormalOnly => AiInspectionTaskScopeMode.AbnormalOnly,
            AiInspectionTaskScopeMode.FocusedOnly => AiInspectionTaskScopeMode.FocusedOnly,
            AiInspectionTaskScopeMode.PendingRecheckOnly => AiInspectionTaskScopeMode.PendingRecheckOnly,
            _ => AiInspectionTaskScopeMode.FullScheme
        };
    }

    private static string NormalizeNote(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private readonly record struct ExecutionWorkItem(string TaskId, string ItemId);

    private readonly record struct TaskExecutionResult(
        string ItemStatus,
        string LastError,
        string ResultSummary,
        bool IsAbnormal,
        string? LinkedInspectionResultId,
        string? LinkedReviewId,
        string? LinkedClosureId,
        string AccentResourceKey);
}
