namespace TylinkInspection.Core.Models;

public sealed class ManualCoordinateRecord
{
    public string DeviceCode { get; init; } = string.Empty;

    public double Longitude { get; init; }

    public double Latitude { get; init; }

    public string Remark { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; init; }

    public string CoordinateSystem { get; init; } = "GCJ-02";
}
