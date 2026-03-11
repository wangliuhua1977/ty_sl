namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeResult
{
    public required InspectionScopeScheme CurrentScheme { get; init; }

    public required InspectionScopeSummary Summary { get; init; }

    public IReadOnlyList<InspectionScopeDevice> Devices { get; init; } = Array.Empty<InspectionScopeDevice>();

    public IReadOnlyList<DirectoryNode> VisibleDirectoryNodes { get; init; } = Array.Empty<DirectoryNode>();

    public IReadOnlyList<InspectionScopeMapPoint> MapPoints { get; init; } = Array.Empty<InspectionScopeMapPoint>();

    public DateTimeOffset GeneratedAt { get; init; }
}
