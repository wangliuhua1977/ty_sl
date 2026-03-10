namespace TylinkInspection.Core.Models;

public sealed class RadarSignal
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Size { get; init; }

    public required double Opacity { get; init; }

    public required string AccentResourceKey { get; init; }
}
