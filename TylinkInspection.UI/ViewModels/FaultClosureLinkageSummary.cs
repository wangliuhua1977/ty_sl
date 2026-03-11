using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class FaultClosureLinkageSummary
{
    private FaultClosureLinkageSummary(FaultClosureRecord? record)
    {
        Record = record;
    }

    public static FaultClosureLinkageSummary Empty { get; } = new(null);

    public FaultClosureRecord? Record { get; }

    public bool HasRecord => Record is not null;

    public string CurrentStatusText => Record?.StatusText ?? "未进入闭环";

    public string ReviewConclusionText => Record?.ReviewConclusionText ?? "--";

    public string LatestRecheckText => Record?.LatestRecheckText ?? "未复检";

    public string AccentResourceKey => Record?.AccentResourceKey ?? "ToneInfoBrush";

    public bool IsPendingDispatch => Record?.IsPendingDispatch == true;

    public bool IsPendingRecheck => Record?.IsAwaitingRecheck == true;

    public bool IsPendingClear => Record is not null &&
        string.Equals(Record.CurrentStatus, FaultClosureStatuses.RecheckPassedPendingClear, StringComparison.OrdinalIgnoreCase);

    public bool IsFalsePositiveClosed => Record is not null &&
        string.Equals(Record.CurrentStatus, FaultClosureStatuses.FalsePositiveClosed, StringComparison.OrdinalIgnoreCase);

    public bool IsCleared => Record is not null &&
        string.Equals(Record.CurrentStatus, FaultClosureStatuses.Cleared, StringComparison.OrdinalIgnoreCase);

    public bool IsClosed => Record is not null &&
        string.Equals(Record.CurrentStatus, FaultClosureStatuses.Closed, StringComparison.OrdinalIgnoreCase);

    public string PendingDispatchText => ToYesNo(IsPendingDispatch);

    public string PendingRecheckText => ToYesNo(IsPendingRecheck);

    public string PendingClearText => ToYesNo(IsPendingClear);

    public string PendingFlagsText
    {
        get
        {
            if (!HasRecord)
            {
                return "未进入闭环";
            }

            var segments = new List<string>();
            if (IsPendingDispatch)
            {
                segments.Add("待派单");
            }

            if (IsPendingRecheck)
            {
                segments.Add("待复检");
            }

            if (IsPendingClear)
            {
                segments.Add("待销警");
            }

            if (IsFalsePositiveClosed)
            {
                segments.Add("误报关闭");
            }

            return segments.Count == 0 ? "无待办闭环动作" : string.Join(" / ", segments);
        }
    }

    public string SummaryText => !HasRecord
        ? "当前点位暂无闭环记录"
        : $"{CurrentStatusText} / {PendingFlagsText}";

    public static FaultClosureLinkageSummary FromRecord(FaultClosureRecord? record)
    {
        return record is null ? Empty : new FaultClosureLinkageSummary(record);
    }

    public static IReadOnlyDictionary<string, FaultClosureLinkageSummary> BuildLookup(IEnumerable<FaultClosureRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Where(record => !string.IsNullOrWhiteSpace(record.DeviceCode))
            .GroupBy(record => record.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => FromRecord(SelectCurrentRecord(group)),
                StringComparer.OrdinalIgnoreCase);
    }

    private static FaultClosureRecord SelectCurrentRecord(IEnumerable<FaultClosureRecord> records)
    {
        return records
            .OrderBy(record => record.IsTerminal ? 1 : 0)
            .ThenByDescending(record => record.UpdatedAt)
            .ThenByDescending(record => record.CreatedAt)
            .First();
    }

    private static string ToYesNo(bool value)
    {
        return value ? "是" : "否";
    }
}
