namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeState
{
    public IReadOnlyList<InspectionScopeScheme> Schemes { get; init; } = Array.Empty<InspectionScopeScheme>();

    public string? ActiveSchemeId { get; init; }
}
