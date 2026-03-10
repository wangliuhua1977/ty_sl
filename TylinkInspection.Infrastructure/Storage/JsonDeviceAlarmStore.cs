using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonDeviceAlarmStore : IDeviceAlarmStore
{
    private readonly string _storagePath;

    public JsonDeviceAlarmStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "device-alarms.json");
    }

    public IReadOnlyList<DeviceAlarmListItem> LoadAll()
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<DeviceAlarmListItem>();
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<List<DeviceAlarmListItem>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? new List<DeviceAlarmListItem>();
    }

    public void SaveAll(IReadOnlyList<DeviceAlarmListItem> alarms)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(alarms, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(_storagePath, json);
    }
}
