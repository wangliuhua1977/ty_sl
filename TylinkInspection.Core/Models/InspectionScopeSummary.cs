namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeSummary
{
    public string SchemeId { get; init; } = string.Empty;

    public string SchemeName { get; init; } = string.Empty;

    public bool IsDefaultScheme { get; init; }

    public int CoveredPointCount { get; init; }

    public int OnlinePointCount { get; init; }

    public int OfflinePointCount { get; init; }

    public int WithCoordinatePointCount { get; init; }

    public int WithoutCoordinatePointCount { get; init; }

    public int FocusPointCount { get; init; }
}
