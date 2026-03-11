namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeScheme
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public IReadOnlyList<InspectionScopeRule> Rules { get; init; } = Array.Empty<InspectionScopeRule>();

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
