using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonCloudPlaybackCacheStore : ICloudPlaybackCacheStore
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonCloudPlaybackCacheStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "cloud-playback-cache.json");
    }

    public IReadOnlyList<CloudPlaybackCacheEntry> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<CloudPlaybackCacheEntry>>(json, _serializerOptions)
            ?? [];
    }

    public void Save(IReadOnlyList<CloudPlaybackCacheEntry> entries)
    {
        EnsureDirectory();
        var json = JsonSerializer.Serialize(entries, _serializerOptions);
        File.WriteAllText(_storagePath, json);
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
