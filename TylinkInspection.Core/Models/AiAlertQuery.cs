namespace TylinkInspection.Core.Models;

public sealed class AiAlertQuery
{
    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }

    public string? DeviceCode { get; init; }

    public IReadOnlyList<int> AlertTypes { get; init; } = Array.Empty<int>();

    public int? AlertSource { get; init; }

    public int PageNo { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public DateTimeOffset? LastSeenTime { get; init; }

    public string? LastSeenId { get; init; }
}
