namespace TylinkInspection.Core.Models;

public static class AiInspectionTaskStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string PartiallyCompleted = "PartiallyCompleted";
    public const string Canceled = "Canceled";
}

public static class AiInspectionTaskItemStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";
}

public static class AiInspectionTaskType
{
    public const string BasicInspection = "BasicInspection";
    public const string PlaybackReview = "PlaybackReview";
    public const string ScreenshotReviewPreparation = "ScreenshotReviewPreparation";
    public const string Recheck = "Recheck";
}

public static class AiInspectionTaskScopeMode
{
    public const string FullScheme = "FullScheme";
    public const string AbnormalOnly = "AbnormalOnly";
    public const string FocusedOnly = "FocusedOnly";
    public const string PendingRecheckOnly = "PendingRecheckOnly";
}

public static class AiInspectionTaskPlanScheduleType
{
    public const string Daily = "Daily";
}

public static class AiInspectionTaskBatchSourceKind
{
    public const string Manual = "Manual";
    public const string Plan = "Plan";
    public const string Retry = "Retry";
    public const string Rerun = "Rerun";
}
