using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IDeviceAlarmStore
{
    IReadOnlyList<DeviceAlarmListItem> LoadAll();

    void SaveAll(IReadOnlyList<DeviceAlarmListItem> alarms);
}
