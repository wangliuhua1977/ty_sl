using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonAiInspectionTaskPlanStore : IAiInspectionTaskPlanStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _storagePath;

    public JsonAiInspectionTaskPlanStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "ai-inspection-task-plans.json");
    }

    public IReadOnlyList<AiInspectionTaskPlan> LoadAll()
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<AiInspectionTaskPlan>();
        }

        var json = File.ReadAllText(_storagePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AiInspectionTaskPlan>();
        }

        return JsonSerializer.Deserialize<List<AiInspectionTaskPlan>>(json, SerializerOptions)
            ?? Array.Empty<AiInspectionTaskPlan>();
    }

    public void SaveAll(IReadOnlyList<AiInspectionTaskPlan> plans)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(plans, SerializerOptions);
        File.WriteAllText(_storagePath, json);
    }
}
