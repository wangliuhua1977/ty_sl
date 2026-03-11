using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class ReportCenterService : IReportCenterService
{
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IInspectionScopeService _inspectionScopeService;
    private readonly IDeviceInspectionStore _deviceInspectionStore;
    private readonly IPlaybackReviewStore _playbackReviewStore;
    private readonly IScreenshotSampleStore _screenshotSampleStore;
    private readonly IManualReviewStore _manualReviewStore;
    private readonly IFaultClosureStore _faultClosureStore;
    private readonly IRecheckTaskStore _recheckTaskStore;

    public ReportCenterService(
        IInspectionScopeService inspectionScopeService,
        IDeviceInspectionStore deviceInspectionStore,
        IPlaybackReviewStore playbackReviewStore,
        IScreenshotSampleStore screenshotSampleStore,
        IManualReviewStore manualReviewStore,
        IFaultClosureStore faultClosureStore,
        IRecheckTaskStore recheckTaskStore)
    {
        _inspectionScopeService = inspectionScopeService;
        _deviceInspectionStore = deviceInspectionStore;
        _playbackReviewStore = playbackReviewStore;
        _screenshotSampleStore = screenshotSampleStore;
        _manualReviewStore = manualReviewStore;
        _faultClosureStore = faultClosureStore;
        _recheckTaskStore = recheckTaskStore;
    }

    public ReportOverview GetOverview(ReportTimeRange timeRange)
    {
        ArgumentNullException.ThrowIfNull(timeRange);

        var normalizedRange = timeRange.Normalize();
        var scope = _inspectionScopeService.GetCurrentScope();
        var deviceCodes = scope.Devices
            .Where(item => !string.IsNullOrWhiteSpace(item.Device.DeviceCode))
            .Select(item => item.Device.DeviceCode.Trim())
            .ToHashSet(TextComparer);

        var inspectionResults = _deviceInspectionStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode) && normalizedRange.Contains(item.InspectionTime))
            .OrderByDescending(item => item.InspectionTime)
            .ToList();
        var playbackReviews = _playbackReviewStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode) && normalizedRange.Contains(item.ReviewedAt))
            .OrderByDescending(item => item.ReviewedAt)
            .ToList();
        var screenshotSamples = _screenshotSampleStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode) && normalizedRange.Contains(item.CapturedAt))
            .OrderByDescending(item => item.CapturedAt)
            .ToList();
        var manualReviews = _manualReviewStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode) && normalizedRange.Contains(item.ReviewedAt))
            .OrderByDescending(item => item.ReviewedAt)
            .ToList();
        var faultClosureRecords = _faultClosureStore.Load()
            .Where(item => deviceCodes.Contains(item.DeviceCode))
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
        var periodFaultClosures = faultClosureRecords
            .Where(item => normalizedRange.Contains(item.UpdatedAt))
            .ToList();
        var recheckTasks = _recheckTaskStore.LoadTasks()
            .Where(item => deviceCodes.Contains(item.DeviceCode))
            .OrderByDescending(item => item.UpdatedAt)
            .ToList();
        var recheckExecutions = _recheckTaskStore.LoadExecutions()
            .Where(item => deviceCodes.Contains(item.DeviceCode) && normalizedRange.Contains(item.CompletedAt))
            .OrderByDescending(item => item.CompletedAt)
            .ToList();

        var inspection = BuildInspectionSummary(scope, normalizedRange, inspectionResults);
        var review = BuildReviewSummary(normalizedRange, playbackReviews, screenshotSamples, manualReviews);
        var faultClosure = BuildFaultClosureSummary(normalizedRange, faultClosureRecords, periodFaultClosures);
        var recheck = BuildRecheckSummary(normalizedRange, recheckTasks, recheckExecutions);
        var generatedAt = DateTimeOffset.Now;
        var reportTitle = ResolveReportTitle(normalizedRange);
        var exportModel = new ReportExportModel
        {
            ReportTitle = reportTitle,
            TimeRange = normalizedRange,
            SchemeId = scope.CurrentScheme.Id,
            SchemeName = scope.CurrentScheme.Name,
            GeneratedAt = generatedAt,
            Inspection = inspection,
            Review = review,
            FaultClosure = faultClosure,
            Recheck = recheck
        };

        return new ReportOverview
        {
            TimeRange = normalizedRange,
            CurrentScheme = scope.CurrentScheme,
            ScopeSummary = scope.Summary,
            GeneratedAt = generatedAt,
            InspectionTotalCount = inspection.TotalCount,
            OnlinePointCount = inspection.OnlineCount,
            OfflinePointCount = inspection.OfflineCount,
            AbnormalPointCount = inspection.AbnormalDeviceCount,
            ManualReviewCount = review.ManualReviewCount,
            PendingDispatchCount = faultClosure.PendingDispatchCount,
            PendingRecheckCount = faultClosure.PendingRecheckCount,
            PendingClearCount = faultClosure.PendingClearCount,
            ClosedCount = faultClosure.ClosedCount + faultClosure.ClearedCount + faultClosure.FalsePositiveClosedCount,
            RecheckExecutionCount = recheck.ExecutionCount,
            RecheckSuccessRate = recheck.SuccessRate,
            StatusMessage = $"当前方案“{scope.CurrentScheme.Name}”已形成 {reportTitle} 统计快照，覆盖 {scope.Summary.CoveredPointCount} 个点位。",
            WarningMessage = inspection.MissingInspectionCount > 0
                ? $"当前方案内仍有 {inspection.MissingInspectionCount} 个点位暂无本地基础巡检结果，巡检统计按已沉淀结果计算。"
                : string.Empty,
            Inspection = inspection,
            Review = review,
            FaultClosure = faultClosure,
            Recheck = recheck,
            ExportModel = exportModel
        };
    }

    private static InspectionSummaryReport BuildInspectionSummary(
        InspectionScopeResult scope,
        ReportTimeRange timeRange,
        IReadOnlyList<DeviceInspectionResult> inspectionResults)
    {
        var distinctDeviceCount = inspectionResults
            .Select(item => item.DeviceCode)
            .Distinct(TextComparer)
            .Count();
        var abnormalDeviceCount = inspectionResults
            .Where(item => item.IsAbnormal)
            .Select(item => item.DeviceCode)
            .Distinct(TextComparer)
            .Count();
        var total = inspectionResults.Count;

        return new InspectionSummaryReport
        {
            CoveredPointCount = scope.Summary.CoveredPointCount,
            TotalCount = total,
            MissingInspectionCount = Math.Max(0, scope.Summary.CoveredPointCount - distinctDeviceCount),
            OnlineCount = inspectionResults.Count(item => item.OnlineStatus == 1),
            OfflineCount = inspectionResults.Count(item => item.OnlineStatus != 1),
            AbnormalDeviceCount = abnormalDeviceCount,
            NeedRecheckCount = inspectionResults.Count(item => item.NeedRecheck),
            PlaybackGradeDistribution =
            [
                BuildSegment("A", "A级", inspectionResults.Count(item => item.PlaybackHealthGrade == PlaybackHealthGrade.A), total),
                BuildSegment("B", "B级", inspectionResults.Count(item => item.PlaybackHealthGrade == PlaybackHealthGrade.B), total),
                BuildSegment("C", "C级", inspectionResults.Count(item => item.PlaybackHealthGrade == PlaybackHealthGrade.C), total),
                BuildSegment("D", "D级", inspectionResults.Count(item => item.PlaybackHealthGrade == PlaybackHealthGrade.D), total),
                BuildSegment("E", "E级", inspectionResults.Count(item => item.PlaybackHealthGrade == PlaybackHealthGrade.E), total)
            ],
            TrendPoints = BuildTrendPoints(timeRange, inspectionResults.Select(item => item.InspectionTime)),
            SummaryText = total == 0
                ? "当前周期内暂无新的基础巡检结果沉淀。"
                : $"当前周期沉淀 {total} 条基础巡检结果，在线 {inspectionResults.Count(item => item.OnlineStatus == 1)} 条，异常点位 {abnormalDeviceCount} 个。"
        };
    }

    private static ReviewSummaryReport BuildReviewSummary(
        ReportTimeRange timeRange,
        IReadOnlyList<PlaybackReviewResult> playbackReviews,
        IReadOnlyList<ScreenshotSampleResult> screenshotSamples,
        IReadOnlyList<ManualReviewRecord> manualReviews)
    {
        var totalReviews = manualReviews.Count;

        return new ReviewSummaryReport
        {
            PlaybackReviewSessionCount = playbackReviews.Count,
            ScreenshotSampleCount = screenshotSamples.Count,
            ManualReviewCount = totalReviews,
            PendingManualReviewCount = manualReviews.Count(item => item.IsPending),
            DispatchSuggestedCount = manualReviews.Count(item => item.RequiresDispatch),
            RecheckSuggestedCount = manualReviews.Count(item => item.RequiresRecheck),
            ConclusionDistribution =
            [
                BuildSegment(ManualReviewConclusions.Pending, "待复核", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.Pending)), totalReviews),
                BuildSegment(ManualReviewConclusions.Normal, "正常", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.Normal)), totalReviews),
                BuildSegment(ManualReviewConclusions.BlackScreen, "黑屏", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.BlackScreen)), totalReviews),
                BuildSegment(ManualReviewConclusions.FrozenFrame, "冻帧", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.FrozenFrame)), totalReviews),
                BuildSegment(ManualReviewConclusions.Tilted, "偏斜", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.Tilted)), totalReviews),
                BuildSegment(ManualReviewConclusions.Obstruction, "遮挡", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.Obstruction)), totalReviews),
                BuildSegment(ManualReviewConclusions.Blur, "模糊", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.Blur)), totalReviews),
                BuildSegment(ManualReviewConclusions.LowLight, "低照度", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.LowLight)), totalReviews),
                BuildSegment(ManualReviewConclusions.FalsePositive, "误报", manualReviews.Count(item => TextComparer.Equals(item.Conclusion, ManualReviewConclusions.FalsePositive)), totalReviews)
            ],
            SourceDistribution =
            [
                BuildSegment(ManualReviewSourceKinds.Live, "直播复核", manualReviews.Count(item => TextComparer.Equals(item.SourceKind, ManualReviewSourceKinds.Live)), totalReviews),
                BuildSegment(ManualReviewSourceKinds.Playback, "回看复核", manualReviews.Count(item => TextComparer.Equals(item.SourceKind, ManualReviewSourceKinds.Playback)), totalReviews),
                BuildSegment(ManualReviewSourceKinds.Ai, "AI复核", manualReviews.Count(item => TextComparer.Equals(item.SourceKind, ManualReviewSourceKinds.Ai)), totalReviews)
            ],
            TrendPoints = BuildTrendPoints(timeRange, manualReviews.Select(item => item.ReviewedAt)),
            SummaryText = $"当前周期沉淀播放复核 {playbackReviews.Count} 场、截图样本 {screenshotSamples.Count} 条、人工复核 {totalReviews} 条。"
        };
    }

    private static FaultClosureSummaryReport BuildFaultClosureSummary(
        ReportTimeRange timeRange,
        IReadOnlyList<FaultClosureRecord> currentRecords,
        IReadOnlyList<FaultClosureRecord> periodRecords)
    {
        var currentTotal = currentRecords.Count;

        return new FaultClosureSummaryReport
        {
            CurrentRecordCount = currentTotal,
            PeriodUpdatedCount = periodRecords.Count,
            CurrentOpenCount = currentRecords.Count(item => !item.IsTerminal),
            PendingDispatchCount = currentRecords.Count(item => item.IsPendingDispatch),
            PendingRecheckCount = currentRecords.Count(item => item.IsAwaitingRecheck),
            PendingClearCount = currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.RecheckPassedPendingClear)),
            ClearedCount = currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.Cleared)),
            ClosedCount = currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.Closed)),
            FalsePositiveClosedCount = currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.FalsePositiveClosed)),
            StatusDistribution =
            [
                BuildSegment(FaultClosureStatuses.PendingDispatch, "待派单", currentRecords.Count(item => item.IsPendingDispatch), currentTotal),
                BuildSegment(FaultClosureStatuses.PendingRecheck, "待复检", currentRecords.Count(item => item.IsAwaitingRecheck), currentTotal),
                BuildSegment(FaultClosureStatuses.RecheckPassedPendingClear, "待销警", currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.RecheckPassedPendingClear)), currentTotal),
                BuildSegment(FaultClosureStatuses.Cleared, "已销警", currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.Cleared)), currentTotal),
                BuildSegment(FaultClosureStatuses.Closed, "已关闭", currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.Closed)), currentTotal),
                BuildSegment(FaultClosureStatuses.FalsePositiveClosed, "误报关闭", currentRecords.Count(item => TextComparer.Equals(item.CurrentStatus, FaultClosureStatuses.FalsePositiveClosed)), currentTotal)
            ],
            SourceDistribution =
            [
                BuildSegment(FaultClosureSourceTypes.LiveReview, "直播复核", currentRecords.Count(item => TextComparer.Equals(item.SourceType, FaultClosureSourceTypes.LiveReview)), currentTotal),
                BuildSegment(FaultClosureSourceTypes.PlaybackReview, "回看复核", currentRecords.Count(item => TextComparer.Equals(item.SourceType, FaultClosureSourceTypes.PlaybackReview)), currentTotal),
                BuildSegment(FaultClosureSourceTypes.AiAlert, "AI告警", currentRecords.Count(item => TextComparer.Equals(item.SourceType, FaultClosureSourceTypes.AiAlert)), currentTotal),
                BuildSegment(FaultClosureSourceTypes.InspectionFailure, "基础巡检", currentRecords.Count(item => TextComparer.Equals(item.SourceType, FaultClosureSourceTypes.InspectionFailure)), currentTotal)
            ],
            TrendPoints = BuildTrendPoints(timeRange, periodRecords.Select(item => item.UpdatedAt)),
            SummaryText = $"当前方案累计沉淀 {currentTotal} 条闭环记录，当前积压待派单 {currentRecords.Count(item => item.IsPendingDispatch)} 条、待复检 {currentRecords.Count(item => item.IsAwaitingRecheck)} 条。"
        };
    }

    private static RecheckSummaryReport BuildRecheckSummary(
        ReportTimeRange timeRange,
        IReadOnlyList<RecheckTaskRecord> tasks,
        IReadOnlyList<RecheckExecutionRecord> executions)
    {
        var executionCount = executions.Count;
        var successCount = executions.Count(item =>
            TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Passed) ||
            TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Completed));
        var failedCount = executions.Count(item => TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Failed));
        var errorCount = executions.Count(item => TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Error));
        var canceledCount = executions.Count(item => TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Canceled));
        var completedCount = executions.Count(item => TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Completed));
        var failureCount = failedCount + errorCount + canceledCount;

        return new RecheckSummaryReport
        {
            TaskCount = tasks.Count,
            EnabledTaskCount = tasks.Count(item => item.IsEnabled),
            RunningTaskCount = tasks.Count(item => item.IsRunning),
            ExecutionCount = executionCount,
            SuccessCount = successCount,
            FailureCount = failureCount,
            ErrorCount = errorCount,
            CanceledCount = canceledCount,
            CompletedCount = completedCount,
            SuccessRate = executionCount == 0 ? 0 : successCount / (double)executionCount,
            FailureRate = executionCount == 0 ? 0 : failureCount / (double)executionCount,
            OutcomeDistribution =
            [
                BuildSegment(RecheckExecutionOutcomes.Passed, "复检通过", executions.Count(item => TextComparer.Equals(item.Outcome, RecheckExecutionOutcomes.Passed)), executionCount),
                BuildSegment(RecheckExecutionOutcomes.Completed, "任务完成", completedCount, executionCount),
                BuildSegment(RecheckExecutionOutcomes.Failed, "复检失败", failedCount, executionCount),
                BuildSegment(RecheckExecutionOutcomes.Error, "执行异常", errorCount, executionCount),
                BuildSegment(RecheckExecutionOutcomes.Canceled, "任务取消", canceledCount, executionCount)
            ],
            TriggerDistribution =
            [
                BuildSegment(RecheckExecutionTriggerTypes.Scheduled, "定时触发", executions.Count(item => TextComparer.Equals(item.TriggerType, RecheckExecutionTriggerTypes.Scheduled)), executionCount),
                BuildSegment(RecheckExecutionTriggerTypes.Manual, "人工触发", executions.Count(item => TextComparer.Equals(item.TriggerType, RecheckExecutionTriggerTypes.Manual)), executionCount),
                BuildSegment(RecheckExecutionTriggerTypes.Retry, "重试触发", executions.Count(item => TextComparer.Equals(item.TriggerType, RecheckExecutionTriggerTypes.Retry)), executionCount),
                BuildSegment(RecheckExecutionTriggerTypes.Recovery, "恢复触发", executions.Count(item => TextComparer.Equals(item.TriggerType, RecheckExecutionTriggerTypes.Recovery)), executionCount)
            ],
            TrendPoints = BuildTrendPoints(timeRange, executions.Select(item => item.CompletedAt)),
            SummaryText = executionCount == 0
                ? $"当前方案已纳管 {tasks.Count} 条复检任务，当前周期内暂无执行记录。"
                : $"当前方案已纳管 {tasks.Count} 条复检任务，周期内执行 {executionCount} 次，成功率 {successCount / (double)executionCount:P1}。"
        };
    }

    private static IReadOnlyList<ReportTrendPoint> BuildTrendPoints(
        ReportTimeRange timeRange,
        IEnumerable<DateTimeOffset> timestamps)
    {
        var normalized = timeRange.Normalize();
        var startDate = normalized.StartTime.ToLocalTime().Date;
        var endDate = normalized.EndTime.AddTicks(-1).ToLocalTime().Date;
        var counts = timestamps
            .Select(item => item.ToLocalTime().Date)
            .GroupBy(item => item)
            .ToDictionary(group => group.Key, group => group.Count());
        var points = new List<ReportTrendPoint>();

        for (var cursor = startDate; cursor <= endDate; cursor = cursor.AddDays(1))
        {
            var bucket = new DateTimeOffset(DateTime.SpecifyKind(cursor, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(cursor));
            var value = counts.GetValueOrDefault(cursor);
            points.Add(new ReportTrendPoint
            {
                Key = cursor.ToString("yyyyMMdd"),
                Label = cursor.ToString("MM-dd"),
                Value = value,
                BucketTime = bucket,
                DetailText = value == 0 ? "当日无新增沉淀" : $"当日沉淀 {value} 条"
            });
        }

        return points;
    }

    private static ReportCountSegment BuildSegment(string key, string label, int count, int total)
    {
        var ratio = total == 0 ? 0 : count / (double)total;
        return new ReportCountSegment
        {
            Key = key,
            Label = label,
            Count = count,
            Ratio = ratio,
            DetailText = total == 0 ? "当前无统计样本" : $"占比 {ratio:P1}"
        };
    }

    private static string ResolveReportTitle(ReportTimeRange timeRange)
    {
        return timeRange.Key switch
        {
            ReportTimeRangePresets.Today => "日报",
            ReportTimeRangePresets.Last7Days => "周报",
            ReportTimeRangePresets.Last30Days => "月报",
            _ => "区间报表"
        };
    }
}
