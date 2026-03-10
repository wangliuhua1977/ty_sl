namespace TylinkInspection.Core.Models;

public sealed class AssistantStatus
{
    public required string Headline { get; init; }

    public required string Detail { get; init; }

    public required string LastAction { get; init; }

    public required string ConfidenceText { get; init; }
}
