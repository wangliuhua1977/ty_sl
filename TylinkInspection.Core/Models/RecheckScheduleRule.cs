using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed record class RecheckScheduleRule
{
    public string RuleId { get; init; } = Guid.NewGuid().ToString("N");

    public string ScopeType { get; init; } = RecheckRuleScopeTypes.GlobalDefault;

    public string ScopeKey { get; init; } = string.Empty;

    public string ScheduleType { get; init; } = RecheckScheduleTypes.FixedDelay;

    public int ScanIntervalSeconds { get; init; } = 30;

    public int InitialDelayMinutes { get; init; } = 5;

    public int IntervalMinutes { get; init; } = 30;

    public int RetryDelayMinutes { get; init; } = 5;

    public int MaxRetryCount { get; init; } = 3;

    public bool IsAutoRecheckEnabled { get; init; } = true;

    public bool AllowManualOverride { get; init; } = true;

    public DateTimeOffset? PlannedRunAt { get; init; }

    public bool UseLightPlaybackReview { get; init; } = true;

    [JsonIgnore]
    public string ScheduleTypeText => RecheckTextMapper.ToScheduleTypeText(ScheduleType);

    [JsonIgnore]
    public string ScopeTypeText => RecheckTextMapper.ToRuleScopeText(ScopeType);

    [JsonIgnore]
    public string ScopeLabelText => string.Equals(ScopeType, RecheckRuleScopeTypes.FaultType, StringComparison.OrdinalIgnoreCase)
        ? $"故障类型 {ScopeKey}"
        : "全局默认规则";

    [JsonIgnore]
    public string RuleSummaryText
    {
        get
        {
            if (string.Equals(ScheduleType, RecheckScheduleTypes.ManualOnly, StringComparison.OrdinalIgnoreCase))
            {
                return $"仅支持手动执行，失败重试 {Math.Max(1, RetryDelayMinutes)} 分钟，最大重试 {Math.Max(0, MaxRetryCount)} 次";
            }

            if (string.Equals(ScheduleType, RecheckScheduleTypes.SpecificTime, StringComparison.OrdinalIgnoreCase))
            {
                return PlannedRunAt.HasValue
                    ? $"指定时间 {PlannedRunAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}，失败重试 {Math.Max(1, RetryDelayMinutes)} 分钟，最大重试 {Math.Max(0, MaxRetryCount)} 次"
                    : "等待指定复检时间";
            }

            return $"首次延迟 {Math.Max(0, InitialDelayMinutes)} 分钟，复检间隔 {Math.Max(1, IntervalMinutes)} 分钟，失败重试 {Math.Max(1, RetryDelayMinutes)} 分钟，最大重试 {Math.Max(0, MaxRetryCount)} 次";
        }
    }

    [JsonIgnore]
    public string AutoExecutionText => IsAutoRecheckEnabled ? "自动复检已启用" : "自动复检已停用";

    [JsonIgnore]
    public string ManualOverrideText => AllowManualOverride ? "允许手动覆盖默认规则" : "当前仅使用默认规则";

    public RecheckScheduleRule Normalize()
    {
        var normalizedType = string.IsNullOrWhiteSpace(ScheduleType)
            ? RecheckScheduleTypes.FixedDelay
            : ScheduleType.Trim();

        return this with
        {
            RuleId = string.IsNullOrWhiteSpace(RuleId) ? Guid.NewGuid().ToString("N") : RuleId.Trim(),
            ScopeType = string.IsNullOrWhiteSpace(ScopeType) ? RecheckRuleScopeTypes.GlobalDefault : ScopeType.Trim(),
            ScopeKey = ScopeKey?.Trim() ?? string.Empty,
            ScheduleType = normalizedType,
            ScanIntervalSeconds = Math.Max(5, ScanIntervalSeconds),
            InitialDelayMinutes = Math.Max(0, InitialDelayMinutes),
            IntervalMinutes = Math.Max(1, IntervalMinutes),
            RetryDelayMinutes = Math.Max(1, RetryDelayMinutes),
            MaxRetryCount = Math.Max(0, MaxRetryCount)
        };
    }

    public static RecheckScheduleRule CreateDefault()
    {
        return new RecheckScheduleRule().Normalize();
    }
}

public static class RecheckScheduleTypes
{
    public const string FixedDelay = "FixedDelay";
    public const string SpecificTime = "SpecificTime";
    public const string ManualOnly = "ManualOnly";
}

public static class RecheckRuleScopeTypes
{
    public const string GlobalDefault = "GlobalDefault";
    public const string FaultType = "FaultType";
    public const string ManualOverride = "ManualOverride";
}

public static class RecheckRuleHitSources
{
    public const string GlobalDefault = "GlobalDefault";
    public const string FaultTypeRule = "FaultTypeRule";
    public const string ManualOverride = "ManualOverride";
}
