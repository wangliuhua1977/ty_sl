namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeMapPoint
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public double? Longitude { get; init; }

    public double? Latitude { get; init; }

    public bool IsInCurrentScope { get; init; }

    public bool IsFocused { get; init; }

    public bool IsOnline { get; init; }

    public InspectionScopeCoordinateSource CoordinateSource { get; init; } = InspectionScopeCoordinateSource.Unknown;

    public string CoordinateSystem { get; init; } = "GCJ-02";

    public PlaybackHealthGrade? PlaybackHealthGrade { get; init; }

    public bool NeedRecheck { get; init; }

    public DateTimeOffset? LastInspectionTime { get; init; }

    public string FailureReasonSummary { get; init; } = string.Empty;
}
