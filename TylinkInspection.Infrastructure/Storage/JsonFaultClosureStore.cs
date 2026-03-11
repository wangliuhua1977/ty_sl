using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonFaultClosureStore : IFaultClosureStore
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonFaultClosureStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "fault-closure-records.json");
    }

    public IReadOnlyList<FaultClosureRecord> Load()
    {
        if (!File.Exists(_storagePath))
        {
            return [];
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<FaultClosureRecord>>(json, _serializerOptions)
            ?? [];
    }

    public void Save(IReadOnlyList<FaultClosureRecord> records)
    {
        EnsureDirectory();
        var json = JsonSerializer.Serialize(records, _serializerOptions);
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
