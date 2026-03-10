using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IDeviceAlarmService
{
    ScrollQueryResult<DeviceAlarmListItem> Query(DeviceAlarmQuery query);
}
