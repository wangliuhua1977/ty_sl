using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IDeviceCatalogService
{
    IReadOnlyList<DirectoryNode> GetDirectoryTree(DirectoryQuery query);

    DeviceListResult GetDeviceList(DeviceListQuery query);

    DevicePointProfile GetDeviceProfile(string deviceCode, DeviceDirectoryItem? seedItem = null);

    IReadOnlyList<DirectoryNode> GetCachedDirectoryTree();

    IReadOnlyList<DeviceDirectoryItem> GetCachedDevices();

    IReadOnlyList<DeviceDirectoryItem> EnsureAllDevicesCached(bool forceRefresh = false);
}
