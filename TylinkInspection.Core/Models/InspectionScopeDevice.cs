namespace TylinkInspection.Core.Models;

public sealed class InspectionScopeDevice
{
    public required DeviceDirectoryItem Device { get; init; }

    public bool IsInCurrentScope { get; init; }

    public bool IsFocused { get; init; }

    public bool IsOnline { get; init; }

    public bool HasCoordinate { get; init; }

    public double? Longitude { get; init; }

    public double? Latitude { get; init; }

    public InspectionScopeCoordinateSource CoordinateSource { get; init; } = InspectionScopeCoordinateSource.Unknown;

    public string CoordinateSystem { get; init; } = "GCJ-02";

    public PlaybackHealthGrade? PlaybackHealthGrade { get; init; }

    public DateTimeOffset? LastInspectionTime { get; init; }

    public string PreferredProtocol { get; init; } = string.Empty;

    public string FallbackProtocol { get; init; } = string.Empty;

    public DateTimeOffset? ExpireTime { get; init; }

    public string VideoEnc { get; init; } = string.Empty;

    public string FailureReason { get; init; } = string.Empty;

    public string Suggestion { get; init; } = string.Empty;

    public bool NeedRecheck { get; init; }

    public DeviceInspectionResult? LatestInspection { get; init; }
}
