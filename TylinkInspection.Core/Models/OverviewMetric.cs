namespace TylinkInspection.Core.Models;

public sealed class OverviewMetric
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public required string Unit { get; init; }

    public required string DeltaText { get; init; }

    public required string AccentResourceKey { get; init; }
}
