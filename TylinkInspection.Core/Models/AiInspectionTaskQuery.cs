namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskQuery
{
    public string? Keyword { get; init; }

    public string? DeviceCode { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}
