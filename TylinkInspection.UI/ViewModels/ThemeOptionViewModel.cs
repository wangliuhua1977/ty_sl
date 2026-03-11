using TylinkInspection.UI.Theming;

namespace TylinkInspection.UI.ViewModels;

public sealed class ThemeOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public required ThemeKind Kind { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required bool IsImplemented { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(StatusAccentResourceKey));
                RaisePropertyChanged(nameof(ActionHint));
            }
        }
    }

    public string StatusText => !IsImplemented
        ? "\u9884\u7559"
        : IsSelected
            ? "\u5f53\u524d"
            : "\u53ef\u7528";

    public string StatusAccentResourceKey => !IsImplemented
        ? "ToneInfoBrush"
        : IsSelected
            ? "TonePrimaryBrush"
            : "ToneSuccessBrush";

    public string ActionHint => !IsImplemented
        ? "\u4e3b\u9898\u8d44\u6e90\u9884\u7559\u4e2d\uff0c\u5f53\u524d\u4e0d\u53ef\u5207\u6362\u3002"
        : IsSelected
            ? "\u5f53\u524d\u6b63\u5728\u4f7f\u7528\u8fd9\u5957\u4e3b\u9898\uff0c\u91cd\u542f\u540e\u4f1a\u81ea\u52a8\u6062\u590d\u3002"
            : "\u70b9\u51fb\u540e\u7acb\u5373\u751f\u6548\uff0c\u5e76\u5199\u5165\u672c\u5730\u4e3b\u9898\u504f\u597d\u3002";
}
