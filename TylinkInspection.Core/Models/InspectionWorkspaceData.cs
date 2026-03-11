namespace TylinkInspection.Core.Models;

public sealed class InspectionWorkspaceData
{
    public required IReadOnlyList<OverviewMetric> OverviewMetrics { get; init; }

    public required CurrentTaskStatus CurrentTask { get; init; }

    public required IReadOnlyList<AlertItem> AlertItems { get; init; }

    public required IReadOnlyList<MapMarker> MapMarkers { get; init; }

    public required AssistantStatus AssistantStatus { get; init; }

    public required IReadOnlyList<ProgressItem> ProgressItems { get; init; }

    public required IReadOnlyList<TodoItem> TodoItems { get; init; }

    public required IReadOnlyList<RadarSignal> RadarSignals { get; init; }

    public required ModulePageData AiInspectionCenterPage { get; init; }

    public required ModulePageData ReviewCenterPage { get; init; }

    public required ModulePageData FaultClosureCenterPage { get; init; }

    public required ModulePageData AiAlertCenterPage { get; init; }

    public required ModulePageData PointGovernancePage { get; init; }

    public required ModulePageData StrategyConfigPage { get; init; }

    public required ModulePageData ReportCenterPage { get; init; }

    public required ModulePageData SystemSettingsPage { get; init; }
}
