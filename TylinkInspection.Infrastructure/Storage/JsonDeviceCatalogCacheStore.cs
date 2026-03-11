using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Infrastructure.Storage;

public sealed class JsonDeviceCatalogCacheStore : IDeviceCatalogCacheStore
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonDeviceCatalogCacheStore(string? storagePath = null)
    {
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TylinkInspection",
            "device-catalog-cache.json");
    }

    public IReadOnlyList<DirectoryNode> LoadDirectoryTree()
    {
        return LoadSnapshot().DirectoryTree;
    }

    public void SaveDirectoryTree(IReadOnlyList<DirectoryNode> nodes)
    {
        var snapshot = LoadSnapshot();
        snapshot.DirectoryTree = nodes.ToList();
        SaveSnapshot(snapshot);
    }

    public IReadOnlyList<DeviceDirectoryItem> LoadDevices()
    {
        return LoadSnapshot().Devices;
    }

    public void UpsertDevices(IReadOnlyList<DeviceDirectoryItem> devices)
    {
        if (devices.Count == 0)
        {
            return;
        }

        var snapshot = LoadSnapshot();
        var merged = snapshot.Devices
            .ToDictionary(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase);

        foreach (var device in devices)
        {
            merged[device.DeviceCode] = device;
        }

        snapshot.Devices = merged.Values
            .OrderBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SaveSnapshot(snapshot);
    }

    public DevicePathInfo? LoadDevicePath(string deviceCode)
    {
        return LoadSnapshot().Paths
            .FirstOrDefault(item => string.Equals(item.DeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveDevicePath(DevicePathInfo pathInfo)
    {
        var snapshot = LoadSnapshot();
        var paths = snapshot.Paths
            .ToDictionary(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase);

        paths[pathInfo.DeviceCode] = pathInfo;
        snapshot.Paths = paths.Values
            .OrderBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        SaveSnapshot(snapshot);
    }

    private DeviceCatalogCacheSnapshot LoadSnapshot()
    {
        if (!File.Exists(_storagePath))
        {
            return new DeviceCatalogCacheSnapshot();
        }

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<DeviceCatalogCacheSnapshot>(json, _serializerOptions)
            ?? new DeviceCatalogCacheSnapshot();
    }

    private void SaveSnapshot(DeviceCatalogCacheSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(snapshot, _serializerOptions);
        File.WriteAllText(_storagePath, json);
    }

    private sealed class DeviceCatalogCacheSnapshot
    {
        public List<DirectoryNode> DirectoryTree { get; set; } = [];

        public List<DeviceDirectoryItem> Devices { get; set; } = [];

        public List<DevicePathInfo> Paths { get; set; } = [];
    }
}
