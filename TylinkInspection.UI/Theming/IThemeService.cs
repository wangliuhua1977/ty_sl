namespace TylinkInspection.UI.Theming;

public interface IThemeService
{
    ThemeDefinition CurrentTheme { get; }

    IReadOnlyList<ThemeDefinition> GetThemes();

    void ApplyTheme(ThemeKind themeKind);
}
