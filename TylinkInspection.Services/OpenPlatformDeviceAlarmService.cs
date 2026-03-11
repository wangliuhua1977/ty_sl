using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class OpenPlatformDeviceAlarmService : OpenPlatformAlarmServiceBase, IDeviceAlarmService
{
    private const string ListEndpoint = "/open/token/device/getDeviceAlarmMessage";

    private readonly IDeviceAlarmStore _deviceAlarmStore;

    public OpenPlatformDeviceAlarmService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient,
        IDeviceAlarmStore deviceAlarmStore)
        : base(optionsProvider, tokenService, openPlatformClient)
    {
        _deviceAlarmStore = deviceAlarmStore;
    }

    public ScrollQueryResult<DeviceAlarmListItem> Query(DeviceAlarmQuery query)
    {
        var response = Execute(ListEndpoint, BuildListParameters(query));
        var unwrapped = response.Data is JsonElement dataElement ? UnwrapResponseData(dataElement) : default;
        var items = ReadArray(unwrapped, "list", "List");

        if (items.Count == 0 && unwrapped.ValueKind == JsonValueKind.Array)
        {
            items = unwrapped.EnumerateArray().ToList();
        }

        var normalizedItems = items
            .Select(MapToAlarm)
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        MergeIntoStore(normalizedItems);

        var mergedItems = _deviceAlarmStore.LoadAll()
            .Where(item => normalizedItems.Any(remote => string.Equals(remote.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.CreateTime)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .ToList();

        var visibleItems = ApplyScrollPagination(mergedItems, query.LastSeenTime, query.LastSeenId).ToList();
        var lastItem = visibleItems.LastOrDefault();
        var pageNo = ReadInt32(unwrapped, "pageNo") ?? query.PageNo;
        var pageSize = ReadInt32(unwrapped, "pageSize") ?? query.PageSize;
        var totalCount = ReadInt32(unwrapped, "total", "totalCount");
        var hasMore = totalCount.HasValue
            ? pageNo * pageSize < totalCount.Value
            : normalizedItems.Count >= query.PageSize;

        return new ScrollQueryResult<DeviceAlarmListItem>
        {
            Items = visibleItems,
            PageNo = pageNo,
            PageSize = pageSize,
            TotalCount = totalCount,
            HasMore = hasMore,
            LastSeenTime = lastItem?.CreateTime,
            LastSeenId = lastItem?.Id
        };
    }

    private IReadOnlyDictionary<string, string> BuildListParameters(DeviceAlarmQuery query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pageNo"] = Math.Max(1, query.PageNo).ToString(),
            ["pageSize"] = Math.Max(1, query.PageSize).ToString()
        };

        if (query.StartTime is not null)
        {
            parameters["startTime"] = FormatOpenPlatformTime(query.StartTime.Value);
        }

        if (query.EndTime is not null)
        {
            parameters["endTime"] = FormatOpenPlatformTime(query.EndTime.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.DeviceCode))
        {
            parameters["deviceCode"] = query.DeviceCode.Trim();
        }

        if (query.AlarmTypes.Count > 0)
        {
            parameters["alertTypeList"] = JoinCsv(query.AlarmTypes);
        }

        if (query.AlertSource is not null)
        {
            parameters["alertSource"] = query.AlertSource.Value.ToString();
        }

        return parameters;
    }

    private void MergeIntoStore(IReadOnlyList<DeviceAlarmListItem> remoteItems)
    {
        var existingItems = _deviceAlarmStore.LoadAll()
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var remoteItem in remoteItems)
        {
            if (existingItems.TryGetValue(remoteItem.Id, out var current))
            {
                existingItems[remoteItem.Id] = new DeviceAlarmListItem
                {
                    Id = current.Id,
                    PlatformAlarmId = remoteItem.PlatformAlarmId,
                    DeviceCode = remoteItem.DeviceCode,
                    DeviceName = remoteItem.DeviceName,
                    AlarmType = remoteItem.AlarmType,
                    AlarmTypeName = remoteItem.AlarmTypeName,
                    Content = remoteItem.Content,
                    CreateTime = remoteItem.CreateTime,
                    UpdateTime = remoteItem.UpdateTime,
                    PlatformStatus = remoteItem.PlatformStatus,
                    PlatformStatusText = remoteItem.PlatformStatusText,
                    SeverityOrStatusText = remoteItem.SeverityOrStatusText,
                    LocalStatus = current.LocalStatus,
                    ReviewNote = current.ReviewNote,
                    AlertSource = remoteItem.AlertSource,
                    AccentResourceKey = remoteItem.AccentResourceKey
                };
            }
            else
            {
                existingItems[remoteItem.Id] = remoteItem;
            }
        }

        var ordered = existingItems.Values
            .OrderByDescending(item => item.CreateTime)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .ToList();

        _deviceAlarmStore.SaveAll(ordered);
    }

    private static IEnumerable<DeviceAlarmListItem> ApplyScrollPagination(IEnumerable<DeviceAlarmListItem> items, DateTimeOffset? lastSeenTime, string? lastSeenId)
    {
        if (lastSeenTime is null || string.IsNullOrWhiteSpace(lastSeenId))
        {
            return items;
        }

        return items.Where(item =>
            item.CreateTime < lastSeenTime.Value ||
            (item.CreateTime == lastSeenTime.Value && string.CompareOrdinal(item.Id, lastSeenId) < 0));
    }

    private static DeviceAlarmListItem MapToAlarm(JsonElement item)
    {
        var platformAlarmId = ReadString(item, "id");
        var alarmType = ReadInt32(item, "alertType", "alarmType") ?? 0;
        var platformStatus = ReadInt32(item, "status");

        return new DeviceAlarmListItem
        {
            Id = NormalizeText(platformAlarmId, Guid.NewGuid().ToString("N")),
            PlatformAlarmId = platformAlarmId,
            DeviceCode = NormalizeText(ReadString(item, "deviceCode"), "--"),
            DeviceName = NormalizeText(ReadString(item, "deviceName"), "\u672a\u547d\u540d\u8bbe\u5907"),
            AlarmType = alarmType,
            AlarmTypeName = ResolveAlarmTypeName(alarmType),
            Content = NormalizeText(ReadString(item, "content"), "\u5e73\u53f0\u672a\u8fd4\u56de\u544a\u8b66\u63cf\u8ff0"),
            CreateTime = ReadDateTimeOffset(item, "createTime", "alertTime") ?? DateTimeOffset.Now,
            UpdateTime = ReadDateTimeOffset(item, "updateTime"),
            PlatformStatus = platformStatus,
            PlatformStatusText = ResolveDeviceAlarmPlatformStatusText(platformStatus),
            SeverityOrStatusText = ResolveDeviceAlarmPlatformStatusText(platformStatus),
            LocalStatus = "Synced",
            ReviewNote = null,
            AlertSource = ReadInt32(item, "alertSource"),
            AccentResourceKey = MapAccent(platformStatus, alarmType)
        };
    }

    private static string ResolveAlarmTypeName(int alarmType)
    {
        return alarmType switch
        {
            1 => "\u8bbe\u5907\u79bb\u7ebf",
            2 => "\u79fb\u52a8\u4fa6\u6d4b",
            10 => "\u8bbe\u5907\u4e0a\u7ebf",
            11 => "\u6709\u4eba\u79fb\u52a8",
            _ => $"\u8bbe\u5907\u544a\u8b66\u7c7b\u578b {alarmType}"
        };
    }

    private static string ResolveDeviceAlarmPlatformStatusText(int? status)
    {
        return status switch
        {
            0 => "\u672a\u8bfb",
            1 => "\u5df2\u8bfb",
            3 => "\u514d\u6253\u6270",
            null => "--",
            _ => $"\u72b6\u6001 {status}"
        };
    }

    private static string MapAccent(int? platformStatus, int alarmType)
    {
        if (alarmType == 10)
        {
            return "ToneSuccessBrush";
        }

        return platformStatus switch
        {
            3 => "ToneFocusBrush",
            1 => "ToneWarningBrush",
            _ => "ToneDangerBrush"
        };
    }
}
