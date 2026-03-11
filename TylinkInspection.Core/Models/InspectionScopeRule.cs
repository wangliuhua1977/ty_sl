namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeRule
{
    public InspectionScopeRuleAction Action { get; init; } = InspectionScopeRuleAction.Include;

    public InspectionScopeTargetType TargetType { get; init; } = InspectionScopeTargetType.Device;

    public string TargetId { get; init; } = string.Empty;

    public string? TargetName { get; init; }

    public string? TargetPath { get; init; }
}
