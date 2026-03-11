namespace TylinkInspection.Core.Models;

public sealed class ReviewCenterQuery
{
    public bool ForceRefreshAiAlerts { get; init; }

    public int AiPageSize { get; init; } = 40;

    public int AiMaxPages { get; init; } = 3;

    public int AiRecentDays { get; init; } = 7;
}
