using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonAiAlertStore : IAiAlertStore
{
    private readonly string _storagePath;

    public JsonAiAlertStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "ai-alerts.json");
    }

    public IReadOnlyList<AiAlertDetail> LoadAll()
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<AiAlertDetail>();
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<AiAlertDetail>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new List<AiAlertDetail>();
    }

    public void SaveAll(IReadOnlyList<AiAlertDetail> alerts)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(alerts, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(_storagePath, json);
    }
}
