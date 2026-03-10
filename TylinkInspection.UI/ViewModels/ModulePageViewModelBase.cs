using System.Collections.ObjectModel;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public abstract class ModulePageViewModelBase : PageViewModelBase
{
    protected ModulePageViewModelBase(ModulePageData pageData)
        : base(pageData.PageTitle, pageData.PageSubtitle)
    {
        StatusBadgeText = pageData.StatusBadgeText;
        StatusBadgeAccentResourceKey = pageData.StatusBadgeAccentResourceKey;
        SummaryCards = new ObservableCollection<OverviewMetric>(pageData.SummaryCards);
        HighlightCards = new ObservableCollection<HighlightCard>(pageData.HighlightCards);
        ActivityItems = new ObservableCollection<ActivityFeedItem>(pageData.ActivityItems);
    }

    public string StatusBadgeText { get; }

    public string StatusBadgeAccentResourceKey { get; }

    public ObservableCollection<OverviewMetric> SummaryCards { get; }

    public ObservableCollection<HighlightCard> HighlightCards { get; }

    public ObservableCollection<ActivityFeedItem> ActivityItems { get; }
}
