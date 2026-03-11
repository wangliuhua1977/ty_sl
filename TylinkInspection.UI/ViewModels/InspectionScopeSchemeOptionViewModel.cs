namespace TylinkInspection.UI.ViewModels;

public sealed class InspectionScopeSchemeOptionViewModel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public string SummaryText { get; init; } = string.Empty;

    public string DisplayName => IsDefault ? $"{Name} · 默认" : Name;
}
