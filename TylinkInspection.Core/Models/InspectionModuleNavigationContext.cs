namespace TylinkInspection.Core.Models;

public sealed class InspectionModuleNavigationContext
{
    public string TargetPageKey { get; init; } = string.Empty;

    public string SourcePageKey { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string TaskItemId { get; init; } = string.Empty;

    public string PlanId { get; init; } = string.Empty;

    public string EvidenceId { get; init; } = string.Empty;

    public string ClosureId { get; init; } = string.Empty;

    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.Now;

    public string ContextSummary { get; init; } = string.Empty;
}

public sealed class InspectionModuleNavigationRequestEventArgs : EventArgs
{
    public required InspectionModuleNavigationContext Context { get; init; }
}

public static class InspectionModulePageKeys
{
    public const string MapInspection = "MapInspection";
    public const string AiInspectionCenter = "AiInspectionCenter";
    public const string ReviewCenter = "ReviewCenter";
    public const string PointGovernance = "PointGovernance";
    public const string FaultClosure = "FaultClosure";
}
