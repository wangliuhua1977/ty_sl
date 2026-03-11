using System.Windows;

namespace TylinkInspection.UI.Theming;

public sealed class ThemeService : IThemeService
{
    private static readonly string[] BaseDictionaryPaths =
    [
        "/TylinkInspection.UI;component/Themes/Base/Spacing.xaml",
        "/TylinkInspection.UI;component/Themes/Base/Typography.xaml",
        "/TylinkInspection.UI;component/Themes/Base/ControlStyles.xaml"
    ];

    private static readonly HashSet<string> ManagedDictionaryPaths =
    [
        .. BaseDictionaryPaths,
        "/TylinkInspection.UI;component/Themes/Variants/TelecomConsole/Theme.xaml",
        "/TylinkInspection.UI;component/Themes/Variants/StandardConsole/Theme.xaml",
        "/TylinkInspection.UI;component/Themes/Variants/TechnologySituation/Theme.xaml",
        "/TylinkInspection.UI;component/Themes/Variants/ExecutiveOverview/Theme.xaml"
    ];

    private readonly ResourceDictionary _applicationResources;
    private readonly IReadOnlyDictionary<ThemeKind, ThemeDefinition> _themes;

    public ThemeService(ResourceDictionary applicationResources)
    {
        _applicationResources = applicationResources;
        _themes = new Dictionary<ThemeKind, ThemeDefinition>
        {
            [ThemeKind.TelecomConsole] = new ThemeDefinition
            {
                Kind = ThemeKind.TelecomConsole,
                DisplayName = "\u7535\u4fe1\u84dd\u767d\u7070\u63a7\u5236\u53f0",
                Description = "\u84dd\u767d\u7070\u653f\u4f01\u8fd0\u7ef4\u98ce\u683c\uff0c\u5f3a\u8c03\u6b63\u5f0f\u3001\u6e05\u723d\u4e0e\u957f\u65f6\u95f4\u4f7f\u7528\u4f53\u9a8c\u3002",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/TelecomConsole/Theme.xaml",
                IsImplemented = true
            },
            [ThemeKind.StandardConsole] = new ThemeDefinition
            {
                Kind = ThemeKind.StandardConsole,
                DisplayName = "\u6807\u51c6\u6d45\u8272\u63a7\u5236\u53f0",
                Description = "\u4e2d\u6027\u6d45\u8272\u8fd0\u7ef4\u53f0\u98ce\u683c\uff0c\u4f5c\u4e3a\u84dd\u767d\u7070\u4e3b\u9898\u4e4b\u5916\u7684\u8f7b\u91cf\u5de5\u4f5c\u9009\u9879\u3002",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/StandardConsole/Theme.xaml",
                IsImplemented = true
            },
            [ThemeKind.TechnologySituation] = new ThemeDefinition
            {
                Kind = ThemeKind.TechnologySituation,
                DisplayName = "\u79d1\u6280\u6001\u52bf\u7248",
                Description = "\u84dd\u9ed1\u79d1\u6280\u6001\u52bf\u98ce\u683c\uff0c\u9002\u5408\u5730\u56fe\u603b\u89c8\u548c\u5927\u5c4f\u5c55\u793a\u573a\u666f\u3002",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/TechnologySituation/Theme.xaml",
                IsImplemented = true
            },
            [ThemeKind.ExecutiveOverview] = new ThemeDefinition
            {
                Kind = ThemeKind.ExecutiveOverview,
                DisplayName = "\u9886\u5bfc\u5c55\u793a\u7248",
                Description = "\u9884\u7559\u7528\u4e8e\u9886\u5bfc\u5c55\u793a\u3001\u6001\u52bf\u6458\u8981\u548c\u7ed3\u679c\u5448\u73b0\u7684\u4e3b\u9898\u8d44\u6e90\u3002",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/ExecutiveOverview/Theme.xaml",
                IsImplemented = false
            }
        };

        CurrentTheme = _themes[ThemePreferenceMapper.DefaultTheme];
    }

    public ThemeDefinition CurrentTheme { get; private set; }

    public IReadOnlyList<ThemeDefinition> GetThemes()
    {
        return _themes.Values.ToList();
    }

    public void ApplyTheme(ThemeKind themeKind)
    {
        var theme = _themes[themeKind];
        if (!theme.IsImplemented)
        {
            throw new InvalidOperationException($"Theme '{theme.DisplayName}' is reserved but not implemented yet.");
        }

        var preservedDictionaries = _applicationResources.MergedDictionaries
            .Where(dictionary => dictionary.Source is null || !ManagedDictionaryPaths.Contains(dictionary.Source.OriginalString))
            .ToList();

        _applicationResources.MergedDictionaries.Clear();
        foreach (var preservedDictionary in preservedDictionaries)
        {
            _applicationResources.MergedDictionaries.Add(preservedDictionary);
        }

        foreach (var dictionaryPath in BaseDictionaryPaths)
        {
            _applicationResources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(dictionaryPath, UriKind.Relative)
            });
        }

        _applicationResources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(theme.ThemeResourcePath, UriKind.Relative)
        });

        CurrentTheme = theme;
    }
}
