using System.Text.Json;
using System.Text.Json.Serialization;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonInspectionScopeStore : IInspectionScopeStore
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public JsonInspectionScopeStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "inspection-scope-schemes.json");
    }

    public InspectionScopeState Load()
    {
        if (!File.Exists(_storagePath))
        {
            return new InspectionScopeState();
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<InspectionScopeState>(json, _serializerOptions)
            ?? new InspectionScopeState();
    }

    public void Save(InspectionScopeState state)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, _serializerOptions);
        File.WriteAllText(_storagePath, json);
    }
}
