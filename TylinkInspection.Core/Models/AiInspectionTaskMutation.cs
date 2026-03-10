namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskMutation
{
    public required string TaskId { get; init; }

    public required string TargetStatus { get; init; }

    public string? Note { get; init; }
}
