namespace TylinkInspection.Core.Models;

public static class FaultClosureStateMachine
{
    public static string ResolveInitialStatus(ManualReviewRecord review)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (string.Equals(review.Conclusion, ManualReviewConclusions.FalsePositive, StringComparison.OrdinalIgnoreCase))
        {
            return FaultClosureStatuses.FalsePositiveClosed;
        }

        if (string.Equals(review.Conclusion, ManualReviewConclusions.Normal, StringComparison.OrdinalIgnoreCase))
        {
            return FaultClosureStatuses.Closed;
        }

        if (review.RequiresDispatch)
        {
            return FaultClosureStatuses.PendingDispatch;
        }

        if (review.RequiresRecheck)
        {
            return FaultClosureStatuses.PendingRecheck;
        }

        return FaultClosureStatuses.PendingDispatch;
    }

    public static bool CanMarkDispatched(string? status)
    {
        return string.Equals(status, FaultClosureStatuses.PendingDispatch, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanRunRecheck(FaultClosureRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return !record.IsTerminal && record.IsAwaitingRecheck;
    }

    public static bool CanClear(string? status)
    {
        return string.Equals(status, FaultClosureStatuses.RecheckPassedPendingClear, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanClose(string? status)
    {
        return !FaultClosureStatuses.IsTerminal(status);
    }

    public static string ResolveAfterDispatch()
    {
        return FaultClosureStatuses.DispatchedPendingProcess;
    }

    public static string ResolveAfterRecheck(bool passed)
    {
        return passed
            ? FaultClosureStatuses.RecheckPassedPendingClear
            : FaultClosureStatuses.PendingRecheck;
    }

    public static bool ShouldAppearInRecheckQueue(FaultClosureRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.IsTerminal)
        {
            return false;
        }

        if (string.Equals(record.CurrentStatus, FaultClosureStatuses.PendingRecheck, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return record.RequiresRecheck &&
               (string.Equals(record.CurrentStatus, FaultClosureStatuses.PendingDispatch, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(record.CurrentStatus, FaultClosureStatuses.DispatchedPendingProcess, StringComparison.OrdinalIgnoreCase));
    }
}
