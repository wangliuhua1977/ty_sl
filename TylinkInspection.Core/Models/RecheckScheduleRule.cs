using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class RecheckScheduleRule
{
    public string RuleId { get; init; } = Guid.NewGuid().ToString("N");

    public string ScheduleType { get; init; } = RecheckScheduleTypes.FixedDelay;

    public int InitialDelayMinutes { get; init; } = 5;

    public int IntervalMinutes { get; init; } = 30;

    public int RetryDelayMinutes { get; init; } = 5;

    public DateTimeOffset? PlannedRunAt { get; init; }

    public bool UseLightPlaybackReview { get; init; } = true;

    [JsonIgnore]
    public string ScheduleTypeText => RecheckTextMapper.ToScheduleTypeText(ScheduleType);

    [JsonIgnore]
    public string RuleSummaryText
    {
        get
        {
            if (string.Equals(ScheduleType, RecheckScheduleTypes.ManualOnly, StringComparison.OrdinalIgnoreCase))
            {
                return "仅支持手动执行";
            }

            if (string.Equals(ScheduleType, RecheckScheduleTypes.SpecificTime, StringComparison.OrdinalIgnoreCase))
            {
                return PlannedRunAt.HasValue
                    ? $"指定时间 {PlannedRunAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
                    : "等待指定复检时间";
            }

            return $"首次延迟 {Math.Max(0, InitialDelayMinutes)} 分钟，后续每 {Math.Max(1, IntervalMinutes)} 分钟复检";
        }
    }

    public static RecheckScheduleRule CreateDefault()
    {
        return new RecheckScheduleRule();
    }
}

public static class RecheckScheduleTypes
{
    public const string FixedDelay = "FixedDelay";
    public const string SpecificTime = "SpecificTime";
    public const string ManualOnly = "ManualOnly";
}
