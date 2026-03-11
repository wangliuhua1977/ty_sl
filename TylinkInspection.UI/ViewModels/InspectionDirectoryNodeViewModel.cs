using System.Collections.ObjectModel;

namespace TylinkInspection.UI.ViewModels;

public sealed class InspectionDirectoryNodeViewModel : ObservableObject
{
    private bool _isChecked;

    public string Id { get; init; } = string.Empty;

    public string? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public int Level { get; init; }

    public bool HasChildren { get; init; }

    public bool HasDevice { get; init; }

    public ObservableCollection<InspectionDirectoryNodeViewModel> Children { get; init; } = [];

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}
