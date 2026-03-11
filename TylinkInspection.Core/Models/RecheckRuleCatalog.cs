namespace TylinkInspection.Core.Models;

public sealed record class RecheckRuleCatalog
{
    public RecheckScheduleRule GlobalDefaultRule { get; init; } = RecheckScheduleRule.CreateDefault();

    public IReadOnlyList<RecheckScheduleRule> FaultTypeRules { get; init; } = Array.Empty<RecheckScheduleRule>();

    public RecheckRuleCatalog Normalize()
    {
        var normalizedGlobalRule = GlobalDefaultRule.Normalize() with
        {
            ScopeType = RecheckRuleScopeTypes.GlobalDefault,
            ScopeKey = string.Empty
        };

        var normalizedFaultTypeRules = FaultTypeRules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ScopeKey))
            .Select(rule => rule.Normalize() with
            {
                ScopeType = RecheckRuleScopeTypes.FaultType,
                ScopeKey = rule.ScopeKey.Trim()
            })
            .GroupBy(rule => rule.ScopeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(rule => rule.ScopeKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RecheckRuleCatalog
        {
            GlobalDefaultRule = normalizedGlobalRule,
            FaultTypeRules = normalizedFaultTypeRules
        };
    }

    public static RecheckRuleCatalog CreateDefault()
    {
        return new RecheckRuleCatalog().Normalize();
    }
}
