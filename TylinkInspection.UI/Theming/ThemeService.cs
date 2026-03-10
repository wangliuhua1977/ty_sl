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
        "/TylinkInspection.UI;component/Themes/Variants/TechnologySituation/Theme.xaml",
        "/TylinkInspection.UI;component/Themes/Variants/StandardConsole/Theme.xaml",
        "/TylinkInspection.UI;component/Themes/Variants/ExecutiveOverview/Theme.xaml"
    ];

    private readonly ResourceDictionary _applicationResources;
    private readonly IReadOnlyDictionary<ThemeKind, ThemeDefinition> _themes;

    public ThemeService(ResourceDictionary applicationResources)
    {
        _applicationResources = applicationResources;
        _themes = new Dictionary<ThemeKind, ThemeDefinition>
        {
            [ThemeKind.TechnologySituation] = new ThemeDefinition
            {
                Kind = ThemeKind.TechnologySituation,
                DisplayName = "科技态势版",
                Description = "蓝黑大屏科技风，适合巡检总览与地图态势场景。",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/TechnologySituation/Theme.xaml",
                IsImplemented = true
            },
            [ThemeKind.StandardConsole] = new ThemeDefinition
            {
                Kind = ThemeKind.StandardConsole,
                DisplayName = "标准控制台版",
                Description = "偏业务运维台风格，强调效率与信息密度。",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/StandardConsole/Theme.xaml",
                IsImplemented = false
            },
            [ThemeKind.ExecutiveOverview] = new ThemeDefinition
            {
                Kind = ThemeKind.ExecutiveOverview,
                DisplayName = "领导展示版",
                Description = "强调摘要、态势和结果呈现的展示风格。",
                ThemeResourcePath = "/TylinkInspection.UI;component/Themes/Variants/ExecutiveOverview/Theme.xaml",
                IsImplemented = false
            }
        };

        CurrentTheme = _themes[ThemeKind.TechnologySituation];
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
            _applicationResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(dictionaryPath, UriKind.Relative) });
        }

        _applicationResources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(theme.ThemeResourcePath, UriKind.Relative) });
        CurrentTheme = theme;
    }
}
