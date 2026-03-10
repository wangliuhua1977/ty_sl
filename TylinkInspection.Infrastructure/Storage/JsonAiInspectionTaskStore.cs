using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonAiInspectionTaskStore : IAiInspectionTaskStore
{
    private readonly string _storagePath;

    public JsonAiInspectionTaskStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "ai-inspection-tasks.json");
    }

    public IReadOnlyList<AiInspectionTaskDetail> LoadAll()
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<AiInspectionTaskDetail>();
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<AiInspectionTaskDetail>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new List<AiInspectionTaskDetail>();
    }

    public void SaveAll(IReadOnlyList<AiInspectionTaskDetail> tasks)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(_storagePath, json);
    }
}
