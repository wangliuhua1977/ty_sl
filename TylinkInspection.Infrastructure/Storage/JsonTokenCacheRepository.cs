using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonTokenCacheRepository : ITokenCacheRepository
{
    private readonly string _storagePath;

    public JsonTokenCacheRepository(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "token-cache.json");
    }

    public OpenPlatformTokenCache? Load(string appId, string enterpriseUser)
    {
        return LoadAll().FirstOrDefault(item =>
            string.Equals(item.AppId, appId, StringComparison.Ordinal) &&
            string.Equals(item.EnterpriseUser, enterpriseUser, StringComparison.Ordinal));
    }

    public void Save(OpenPlatformTokenCache tokenCache)
    {
        var items = LoadAll();
        var index = items.FindIndex(item =>
            string.Equals(item.AppId, tokenCache.AppId, StringComparison.Ordinal) &&
            string.Equals(item.EnterpriseUser, tokenCache.EnterpriseUser, StringComparison.Ordinal));

        if (index >= 0)
        {
            items[index] = tokenCache;
        }
        else
        {
            items.Add(tokenCache);
        }

        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(_storagePath, json);
    }

    private List<OpenPlatformTokenCache> LoadAll()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var json = File.ReadAllText(_storagePath);
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<OpenPlatformTokenCache>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? [];
        }

        var legacyItem = JsonSerializer.Deserialize<OpenPlatformTokenCache>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (legacyItem is null)
        {
            return [];
        }

        return [legacyItem];
    }
}
