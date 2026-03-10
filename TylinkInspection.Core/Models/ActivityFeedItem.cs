namespace TylinkInspection.Core.Models;

public sealed class ActivityFeedItem
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string MetaText { get; init; }

    public required string AccentResourceKey { get; init; }
}
