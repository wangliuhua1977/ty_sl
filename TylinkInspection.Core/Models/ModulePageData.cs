namespace TylinkInspection.Core.Models;

public sealed class ModulePageData
{
    public required string PageTitle { get; init; }

    public required string PageSubtitle { get; init; }

    public required string StatusBadgeText { get; init; }

    public required string StatusBadgeAccentResourceKey { get; init; }

    public required IReadOnlyList<OverviewMetric> SummaryCards { get; init; }

    public required IReadOnlyList<HighlightCard> HighlightCards { get; init; }

    public required IReadOnlyList<ActivityFeedItem> ActivityItems { get; init; }
}
