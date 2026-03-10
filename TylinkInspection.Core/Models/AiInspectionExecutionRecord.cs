namespace TylinkInspection.Core.Models;

public sealed class AiInspectionExecutionRecord
{
    public required string RecordId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string Message { get; init; }

    public required string AccentResourceKey { get; init; }
}
