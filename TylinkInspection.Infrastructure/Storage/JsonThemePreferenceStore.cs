using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonThemePreferenceStore : IThemePreferenceStore
{
    private readonly string _storagePath;

    public JsonThemePreferenceStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "theme-preference.json");
    }

    public ThemePreference? Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return null;
            }

            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<ThemePreference>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(ThemePreference preference)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(preference, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        });

        File.WriteAllText(_storagePath, json);
    }
}
