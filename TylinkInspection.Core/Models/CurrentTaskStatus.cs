namespace TylinkInspection.Core.Models;

public sealed class CurrentTaskStatus
{
    public required string Title { get; init; }

    public required string RegionName { get; init; }

    public required string TimeWindow { get; init; }

    public required double CompletionRate { get; init; }

    public required int CompletedCount { get; init; }

    public required int AbnormalCount { get; init; }

    public required int PendingReviewCount { get; init; }
}
