namespace TylinkInspection.Core.Models;

public sealed class HighlightCard
{
    public required string Title { get; init; }

    public required string Headline { get; init; }

    public required string Description { get; init; }

    public required string AccentResourceKey { get; init; }
}
