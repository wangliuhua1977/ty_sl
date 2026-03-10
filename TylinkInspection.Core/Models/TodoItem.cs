namespace TylinkInspection.Core.Models;

public sealed class TodoItem
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string DueText { get; init; }

    public required string AccentResourceKey { get; init; }
}
