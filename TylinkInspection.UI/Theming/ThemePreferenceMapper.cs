namespace TylinkInspection.UI.Theming;

public static class ThemePreferenceMapper
{
    public static ThemeKind DefaultTheme => ThemeKind.TelecomConsole;

    public static string ToStorageKey(ThemeKind themeKind)
    {
        return themeKind.ToString();
    }

    public static ThemeKind ResolveStoredOrDefault(IThemeService themeService, string? themeKey)
    {
        if (TryParseImplemented(themeService, themeKey, out var themeKind))
        {
            return themeKind;
        }

        return DefaultTheme;
    }

    public static bool TryParseImplemented(IThemeService themeService, string? themeKey, out ThemeKind themeKind)
    {
        if (Enum.TryParse<ThemeKind>(themeKey, ignoreCase: true, out var parsedThemeKind))
        {
            themeKind = parsedThemeKind;
            return themeService.GetThemes().Any(theme => theme.Kind == parsedThemeKind && theme.IsImplemented);
        }

        themeKind = DefaultTheme;
        return false;
    }
}
