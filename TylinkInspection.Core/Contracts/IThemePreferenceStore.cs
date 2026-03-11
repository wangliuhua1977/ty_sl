using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IThemePreferenceStore
{
    ThemePreference? Load();

    void Save(ThemePreference preference);
}
