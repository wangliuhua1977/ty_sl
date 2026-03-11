using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonPlaybackReviewStore : IPlaybackReviewStore
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonPlaybackReviewStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "playback-review-results.json");
    }

    public IReadOnlyList<PlaybackReviewResult> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<PlaybackReviewResult>>(json, _serializerOptions)
            ?? [];
    }

    public void Save(IReadOnlyList<PlaybackReviewResult> results)
    {
        EnsureDirectory();
        var json = JsonSerializer.Serialize(results, _serializerOptions);
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
