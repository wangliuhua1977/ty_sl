using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class LocalDeviceAlarmService : IDeviceAlarmService
{
    private readonly IDeviceAlarmStore _alarmStore;

    public LocalDeviceAlarmService(IDeviceAlarmStore alarmStore)
    {
        _alarmStore = alarmStore;
        EnsureSeedData();
    }

    public ScrollQueryResult<DeviceAlarmListItem> Query(DeviceAlarmQuery query)
    {
        var filtered = _alarmStore.LoadAll().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.DeviceCode))
        {
            filtered = filtered.Where(item => item.DeviceCode.Contains(query.DeviceCode, StringComparison.OrdinalIgnoreCase));
        }

        if (query.AlarmTypes.Count > 0)
        {
            filtered = filtered.Where(item => query.AlarmTypes.Contains(item.AlarmType));
        }

        if (query.AlertSource is not null)
        {
            filtered = filtered.Where(item => item.AlertSource == query.AlertSource.Value);
        }

        if (query.StartTime is not null)
        {
            filtered = filtered.Where(item => item.CreateTime >= query.StartTime.Value);
        }

        if (query.EndTime is not null)
        {
            filtered = filtered.Where(item => item.CreateTime <= query.EndTime.Value);
        }

        var items = ApplyScrollPagination(
                filtered.OrderByDescending(item => item.CreateTime).ThenByDescending(item => item.Id, StringComparer.Ordinal),
                query.LastSeenTime,
                query.LastSeenId)
            .Take(query.PageSize)
            .ToList();

        var lastItem = items.LastOrDefault();
        return new ScrollQueryResult<DeviceAlarmListItem>
        {
            Items = items,
            PageNo = query.PageNo,
            PageSize = query.PageSize,
            TotalCount = _alarmStore.LoadAll().Count,
            HasMore = items.Count >= query.PageSize,
            LastSeenTime = lastItem?.CreateTime,
            LastSeenId = lastItem?.Id
        };
    }

    private void EnsureSeedData()
    {
        if (_alarmStore.LoadAll().Count > 0)
        {
            return;
        }

        _alarmStore.SaveAll(
        [
            BuildAlarm("D-1001", "TY-SH-0001", "虹桥枢纽 01", 1, "设备离线", "设备持续离线超过 180 秒。", DateTimeOffset.Now.AddMinutes(-12), 1, "未读", "ToneDangerBrush"),
            BuildAlarm("D-1002", "TY-SZ-0017", "苏州园区 17", 2, "移动侦测", "检测到非预期画面位移，待与 AI 告警交叉复核。", DateTimeOffset.Now.AddMinutes(-30), 1, "已读", "ToneWarningBrush"),
            BuildAlarm("D-1003", "TY-HZ-0008", "杭州西站 08", 10, "设备上线", "设备已恢复在线，等待业务巡检回写。", DateTimeOffset.Now.AddHours(-1), 1, "已读", "ToneSuccessBrush"),
            BuildAlarm("D-1004", "TY-NB-0012", "宁波港区", 3, "视频遮挡", "普通告警识别到画面遮挡，需要与 AI 告警详情联动复核。", DateTimeOffset.Now.AddHours(-2), 2, "免打扰", "ToneInfoBrush"),
            BuildAlarm("D-1005", "TY-HF-0022", "合肥南广场", 7, "码流异常", "码流抖动导致画面稳定性下降。", DateTimeOffset.Now.AddHours(-3), 3, "已读", "ToneFocusBrush")
        ]);
    }

    private static IEnumerable<DeviceAlarmListItem> ApplyScrollPagination(
        IEnumerable<DeviceAlarmListItem> items,
        DateTimeOffset? lastSeenTime,
        string? lastSeenId)
    {
        if (lastSeenTime is null || string.IsNullOrWhiteSpace(lastSeenId))
        {
            return items;
        }

        return items.Where(item =>
            item.CreateTime < lastSeenTime.Value ||
            (item.CreateTime == lastSeenTime.Value && string.CompareOrdinal(item.Id, lastSeenId) < 0));
    }

    private static DeviceAlarmListItem BuildAlarm(
        string id,
        string deviceCode,
        string deviceName,
        int alarmType,
        string alarmTypeName,
        string content,
        DateTimeOffset createTime,
        int alertSource,
        string severityText,
        string accentResourceKey)
    {
        return new DeviceAlarmListItem
        {
            Id = id,
            PlatformAlarmId = id,
            DeviceCode = deviceCode,
            DeviceName = deviceName,
            AlarmType = alarmType,
            AlarmTypeName = alarmTypeName,
            Content = content,
            CreateTime = createTime,
            UpdateTime = createTime.AddMinutes(2),
            PlatformStatus = severityText == "未读" ? 0 : severityText == "免打扰" ? 3 : 1,
            PlatformStatusText = severityText,
            SeverityOrStatusText = severityText,
            LocalStatus = "Synced",
            ReviewNote = null,
            AlertSource = alertSource,
            AccentResourceKey = accentResourceKey
        };
    }
}
