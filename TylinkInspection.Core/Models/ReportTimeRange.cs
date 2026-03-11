namespace TylinkInspection.Core.Models;

public sealed class ReportTimeRange
{
    public string Key { get; init; } = ReportTimeRangePresets.Today;

    public string Label { get; init; } = "今日";

    public DateTimeOffset StartTime { get; init; } = CreateLocalOffset(DateTime.Now.Date);

    public DateTimeOffset EndTime { get; init; } = DateTimeOffset.Now;

    public bool IsCustom { get; init; }

    public string DisplayText
    {
        get
        {
            var displayEnd = EndTime.AddTicks(-1);
            return $"{StartTime.ToLocalTime():yyyy-MM-dd HH:mm} ~ {displayEnd.ToLocalTime():yyyy-MM-dd HH:mm}";
        }
    }

    public bool Contains(DateTimeOffset timestamp)
    {
        return timestamp >= StartTime && timestamp < EndTime;
    }

    public int DaySpan
    {
        get
        {
            var endDate = EndTime.AddTicks(-1).ToLocalTime().Date;
            var startDate = StartTime.ToLocalTime().Date;
            return Math.Max(1, (endDate - startDate).Days + 1);
        }
    }

    public ReportTimeRange Normalize()
    {
        if (EndTime <= StartTime)
        {
            throw new InvalidOperationException("报表时间范围无效，结束时间必须晚于开始时间。");
        }

        return new ReportTimeRange
        {
            Key = string.IsNullOrWhiteSpace(Key) ? ReportTimeRangePresets.Custom : Key.Trim(),
            Label = string.IsNullOrWhiteSpace(Label) ? "自定义" : Label.Trim(),
            StartTime = StartTime,
            EndTime = EndTime,
            IsCustom = IsCustom || string.Equals(Key, ReportTimeRangePresets.Custom, StringComparison.OrdinalIgnoreCase)
        };
    }

    public static ReportTimeRange CreateToday(DateTimeOffset? now = null)
    {
        var current = (now ?? DateTimeOffset.Now).ToLocalTime();
        var start = CreateLocalOffset(current.Date);
        return new ReportTimeRange
        {
            Key = ReportTimeRangePresets.Today,
            Label = "今日",
            StartTime = start,
            EndTime = current,
            IsCustom = false
        };
    }

    public static ReportTimeRange CreateLast7Days(DateTimeOffset? now = null)
    {
        var current = (now ?? DateTimeOffset.Now).ToLocalTime();
        var start = CreateLocalOffset(current.Date.AddDays(-6));
        return new ReportTimeRange
        {
            Key = ReportTimeRangePresets.Last7Days,
            Label = "最近7天",
            StartTime = start,
            EndTime = current,
            IsCustom = false
        };
    }

    public static ReportTimeRange CreateLast30Days(DateTimeOffset? now = null)
    {
        var current = (now ?? DateTimeOffset.Now).ToLocalTime();
        var start = CreateLocalOffset(current.Date.AddDays(-29));
        return new ReportTimeRange
        {
            Key = ReportTimeRangePresets.Last30Days,
            Label = "最近30天",
            StartTime = start,
            EndTime = current,
            IsCustom = false
        };
    }

    public static ReportTimeRange CreateCustom(DateTime startDate, DateTime endDate)
    {
        var normalizedStart = startDate.Date;
        var normalizedEndExclusive = endDate.Date.AddDays(1);
        if (normalizedEndExclusive <= normalizedStart)
        {
            throw new InvalidOperationException("自定义时间范围无效，结束日期必须不早于开始日期。");
        }

        return new ReportTimeRange
        {
            Key = ReportTimeRangePresets.Custom,
            Label = "自定义",
            StartTime = CreateLocalOffset(normalizedStart),
            EndTime = CreateLocalOffset(normalizedEndExclusive),
            IsCustom = true
        };
    }

    private static DateTimeOffset CreateLocalOffset(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified));
    }
}

public static class ReportTimeRangePresets
{
    public const string Today = "Today";
    public const string Last7Days = "Last7Days";
    public const string Last30Days = "Last30Days";
    public const string Custom = "Custom";
}
