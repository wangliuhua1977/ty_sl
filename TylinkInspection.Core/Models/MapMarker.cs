namespace TylinkInspection.Core.Models;

public sealed class MapMarker
{
    public required string Name { get; init; }

    public required string StateLabel { get; init; }

    public required double X { get; init; }

    public required double Y { get; init; }

    public required string AccentBrushKey { get; init; }

    public required string GlowBrushKey { get; init; }
}
