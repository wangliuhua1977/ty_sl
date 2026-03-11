using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IDeviceCatalogCacheStore
{
    IReadOnlyList<DirectoryNode> LoadDirectoryTree();

    void SaveDirectoryTree(IReadOnlyList<DirectoryNode> nodes);

    IReadOnlyList<DeviceDirectoryItem> LoadDevices();

    void UpsertDevices(IReadOnlyList<DeviceDirectoryItem> devices);

    DevicePathInfo? LoadDevicePath(string deviceCode);

    void SaveDevicePath(DevicePathInfo pathInfo);
}
