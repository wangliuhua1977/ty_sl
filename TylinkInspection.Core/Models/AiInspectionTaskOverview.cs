namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskOverview
{
    public int TotalTaskCount { get; init; }

    public int PendingTaskCount { get; init; }

    public int RunningTaskCount { get; init; }

    public int SucceededTaskCount { get; init; }

    public int FailedTaskCount { get; init; }

    public int PartiallyCompletedTaskCount { get; init; }

    public int CanceledTaskCount { get; init; }

    public int TotalItemCount { get; init; }

    public int AbnormalItemCount { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    public string StatusMessage { get; init; } = string.Empty;
}
