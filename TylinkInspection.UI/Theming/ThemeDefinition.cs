namespace TylinkInspection.UI.Theming;

public sealed class ThemeDefinition
{
    public required ThemeKind Kind { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required string ThemeResourcePath { get; init; }

    public required bool IsImplemented { get; init; }
}
