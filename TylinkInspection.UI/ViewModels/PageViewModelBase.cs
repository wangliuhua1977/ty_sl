namespace TylinkInspection.UI.ViewModels;

public abstract class PageViewModelBase : ObservableObject
{
    protected PageViewModelBase(string pageTitle, string pageSubtitle)
    {
        PageTitle = pageTitle;
        PageSubtitle = pageSubtitle;
    }

    public string PageTitle { get; }

    public string PageSubtitle { get; }
}
