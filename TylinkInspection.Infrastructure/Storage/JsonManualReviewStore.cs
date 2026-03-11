using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonManualReviewStore : IManualReviewStore
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonManualReviewStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "manual-review-records.json");
    }

    public IReadOnlyList<ManualReviewRecord> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<ManualReviewRecord>>(json, _serializerOptions)
            ?? [];
    }

    public void Save(IReadOnlyList<ManualReviewRecord> reviews)
    {
        EnsureDirectory();
        var json = JsonSerializer.Serialize(reviews, _serializerOptions);
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
