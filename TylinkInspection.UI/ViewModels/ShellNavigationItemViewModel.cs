namespace TylinkInspection.UI.ViewModels;

public sealed class ShellNavigationItemViewModel : ObservableObject
{
    private bool _isSelected;

    public required string Title { get; init; }

    public required PageViewModelBase PageViewModel { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
