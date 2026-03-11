namespace TylinkInspection.UI.ViewModels;

public sealed class SelectionItemViewModel : ObservableObject
{
    private bool _isSelected;

    public string? Key { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public string? Badge { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
