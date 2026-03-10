namespace TylinkInspection.Core.Models;

public sealed class ProgressItem
{
    public required string Label { get; init; }

    public required string Value { get; init; }

    public required string AccentResourceKey { get; init; }
}
