namespace TylinkInspection.UI.ViewModels;

public sealed class ReportVisualItemViewModel
{
    public string Label { get; init; } = string.Empty;

    public string ValueText { get; init; } = string.Empty;

    public string DetailText { get; init; } = string.Empty;

    public string AccentResourceKey { get; init; } = "ToneInfoBrush";

    public double BarWidth { get; init; }
}
