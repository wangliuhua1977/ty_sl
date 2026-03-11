using System.Globalization;
using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class DeviceCatalogService : OpenPlatformAlarmServiceBase, IDeviceCatalogService
{
    private const string DirectoryTreeEndpoint = "/open/token/device/getReginWithGroupList";
    private const string DirectoryDeviceListEndpoint = "/open/token/device/getDeviceList";
    private const string AllDeviceListEndpoint = "/open/token/device/getAllDeviceListNew";
    private const string DeviceInfoEndpoint = "/open/token/device/getDeviceInfoByDeviceCode";
    private const string DeviceResourceEndpoint = "/open/token/device/getDeviceResource";
    private const string DevicePathEndpoint = "/open/token/vcpTree/getAllPath";
    private const string DeviceStatusEndpoint = "/open/token/device/batchDeviceStatus";
    private const string DeviceConnectInfoEndpoint = "/open/token/vpaas/device/batch/getConnectInfo";
    private const string AllDevicesLabel = "全量设备池";
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IDeviceCatalogCacheStore _cacheStore;

    public DeviceCatalogService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient,
        IDeviceCatalogCacheStore cacheStore)
        : base(optionsProvider, tokenService, openPlatformClient)
    {
        _cacheStore = cacheStore;
    }

    public IReadOnlyList<DirectoryNode> GetDirectoryTree(DirectoryQuery query)
    {
        var cachedTree = _cacheStore.LoadDirectoryTree();
        if (!query.ForceRefresh && string.IsNullOrWhiteSpace(query.ParentRegionId) && cachedTree.Count > 0)
        {
            return cachedTree;
        }

        try
        {
            var visited = new HashSet<string>(TextComparer);
            var nodes = LoadDirectoryChildren(query.ParentRegionId, null, query.Recursive, visited);
            if (string.IsNullOrWhiteSpace(query.ParentRegionId))
            {
                _cacheStore.SaveDirectoryTree(nodes);
            }

            return nodes;
        }
        catch
        {
            if (!query.ForceRefresh && cachedTree.Count > 0)
            {
                return cachedTree;
            }

            throw;
        }
    }

    public DeviceListResult GetDeviceList(DeviceListQuery query)
    {
        return query.Scope == DeviceListScope.All
            ? GetAllDevices(query)
            : GetDirectoryDevices(query);
    }

    public DevicePointProfile GetDeviceProfile(string deviceCode, DeviceDirectoryItem? seedItem = null)
    {
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            throw new ArgumentException("设备编码不能为空。", nameof(deviceCode));
        }

        var normalizedCode = deviceCode.Trim();
        var cachedDevice = _cacheStore.LoadDevices()
            .FirstOrDefault(item => TextComparer.Equals(item.DeviceCode, normalizedCode));
        var pathFromCache = _cacheStore.LoadDevicePath(normalizedCode);

        DeviceDirectoryItem? mergedItem = seedItem ?? cachedDevice;
        DevicePathInfo? pathInfo = pathFromCache;

        try
        {
            var detailResponse = Execute(DeviceInfoEndpoint, new Dictionary<string, string>
            {
                ["deviceCode"] = normalizedCode
            });

            var detail = UnwrapResponseData(detailResponse.Data);
            var connectInfo = LoadConnectInfo(new[] { normalizedCode }).GetValueOrDefault(normalizedCode);
            var statusInfo = LoadStatusInfo(new[] { normalizedCode }).GetValueOrDefault(normalizedCode);
            var deviceSource = LoadDeviceSource(normalizedCode);

            mergedItem = ResolveDirectoryAssignment(MergeDeviceItem(
                mergedItem,
                CreateBaseDeviceItem(detail, normalizedCode),
                statusInfo,
                connectInfo,
                deviceSource,
                mergedItem?.DirectoryId,
                mergedItem?.DirectoryName,
                mergedItem?.DirectoryPath));

            var pathResponse = Execute(DevicePathEndpoint, new Dictionary<string, string>
            {
                ["deviceCode"] = normalizedCode
            });

            pathInfo = MapPathInfo(UnwrapResponseData(pathResponse.Data), normalizedCode);

            _cacheStore.UpsertDevices([mergedItem]);
            _cacheStore.SaveDevicePath(pathInfo);
        }
        catch
        {
            if (mergedItem is null)
            {
                throw;
            }

            pathInfo ??= CreateEmptyPath(normalizedCode);
        }

        return new DevicePointProfile
        {
            Device = mergedItem ?? throw new InvalidOperationException("未找到设备基础信息。"),
            PathInfo = pathInfo ?? CreateEmptyPath(normalizedCode)
        };
    }

    public IReadOnlyList<DirectoryNode> GetCachedDirectoryTree()
    {
        return _cacheStore.LoadDirectoryTree();
    }

    public IReadOnlyList<DeviceDirectoryItem> GetCachedDevices()
    {
        return _cacheStore.LoadDevices();
    }

    public IReadOnlyList<DeviceDirectoryItem> EnsureAllDevicesCached(bool forceRefresh = false)
    {
        var cachedDevices = _cacheStore.LoadDevices();
        if (!forceRefresh && cachedDevices.Count > 0)
        {
            return cachedDevices;
        }

        try
        {
            var visitedCursors = new HashSet<long>();
            var pageNo = 1;
            var cursor = 0L;
            var collected = new List<DeviceDirectoryItem>();

            while (true)
            {
                var page = GetAllDevices(new DeviceListQuery
                {
                    Scope = DeviceListScope.All,
                    PageNo = pageNo,
                    PageSize = 100,
                    LastId = cursor,
                    ForceRefresh = forceRefresh
                });

                if (page.Items.Count == 0)
                {
                    break;
                }

                collected.AddRange(page.Items);
                if (!page.HasMore || !page.NextCursor.HasValue || !visitedCursors.Add(page.NextCursor.Value))
                {
                    break;
                }

                cursor = page.NextCursor.Value;
                pageNo++;
            }

            var devices = _cacheStore.LoadDevices();
            if (devices.Count > 0)
            {
                return devices;
            }

            return collected
                .GroupBy(item => item.DeviceCode, TextComparer)
                .Select(group => group.Last())
                .OrderBy(item => item.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DeviceCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return cachedDevices.Count > 0 ? cachedDevices : [];
        }
    }

    private DeviceListResult GetDirectoryDevices(DeviceListQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.RegionId))
        {
            return new DeviceListResult
            {
                PageNo = 1,
                PageSize = query.PageSize
            };
        }

        var response = Execute(DirectoryDeviceListEndpoint, new Dictionary<string, string>
        {
            ["regionId"] = query.RegionId.Trim(),
            ["pageNo"] = Math.Max(1, query.PageNo).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(query.PageSize, 1, 50).ToString(CultureInfo.InvariantCulture)
        });

        var payload = UnwrapResponseData(response.Data);
        var pageNo = ReadInt32(payload, "pageNo") ?? Math.Max(1, query.PageNo);
        var pageSize = ReadInt32(payload, "pageSize") ?? Math.Clamp(query.PageSize, 1, 50);
        var totalCount = ReadInt32(payload, "totalCount");
        var directoryName = ResolveDirectoryName(query.RegionId, query.RegionName);
        var directoryPath = ResolveDirectoryPath(query.RegionId, query.DirectoryPath, directoryName);

        var items = payload.ValueKind == JsonValueKind.Object
            ? ReadArray(payload, "list")
                .Select(item => CreateBaseDeviceItem(item))
                .ToList()
            : [];

        var enrichedItems = EnrichDevices(items, query.RegionId, directoryName, directoryPath);

        return new DeviceListResult
        {
            Items = enrichedItems,
            PageNo = pageNo,
            PageSize = pageSize,
            TotalCount = totalCount,
            HasMore = totalCount.HasValue && pageNo * pageSize < totalCount.Value
        };
    }

    private DeviceListResult GetAllDevices(DeviceListQuery query)
    {
        var parameters = new Dictionary<string, string>
        {
            ["lastId"] = Math.Max(0, query.LastId).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(query.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["hasChildDevices"] = "0"
        };

        if (!string.IsNullOrWhiteSpace(query.RegionId))
        {
            parameters["cusRegionId"] = query.RegionId.Trim();
        }

        var response = Execute(AllDeviceListEndpoint, parameters);
        var payload = UnwrapResponseData(response.Data);
        var nextCursor = ReadInt64(payload, "lastId");
        var totalCount = ReadInt64(payload, "total") is { } total ? (int?)Convert.ToInt32(total, CultureInfo.InvariantCulture) : null;
        var items = ReadArray(payload, "list")
            .Select(item => new DeviceDirectoryItem
            {
                DeviceCode = ReadString(item, "deviceCode"),
                DeviceName = PickText(ReadString(item, "deviceName"), "未命名设备"),
                DirectoryId = query.RegionId ?? string.Empty,
                DirectoryName = ResolveDirectoryName(query.RegionId, query.RegionName, AllDevicesLabel),
                DirectoryPath = ResolveDirectoryPath(query.RegionId, query.DirectoryPath, ResolveDirectoryName(query.RegionId, query.RegionName, AllDevicesLabel)),
                RegionGbId = ReadString(item, "regionGBId"),
                GbId = ReadString(item, "gbId"),
                SourceGbId = ReadString(item, "sourceGbId")
            })
            .ToList();

        var enrichedItems = EnrichDevices(
            items,
            query.RegionId,
            ResolveDirectoryName(query.RegionId, query.RegionName, AllDevicesLabel),
            ResolveDirectoryPath(query.RegionId, query.DirectoryPath, ResolveDirectoryName(query.RegionId, query.RegionName, AllDevicesLabel)));

        return new DeviceListResult
        {
            Items = enrichedItems,
            PageNo = Math.Max(1, query.PageNo),
            PageSize = Math.Clamp(query.PageSize, 1, 100),
            TotalCount = totalCount,
            NextCursor = nextCursor is > -1 ? nextCursor : null,
            HasMore = nextCursor is > -1
        };
    }

    private IReadOnlyList<DirectoryNode> LoadDirectoryChildren(
        string? parentRegionId,
        string? parentPath,
        bool recursive,
        ISet<string> visited)
    {
        var response = Execute(DirectoryTreeEndpoint, new Dictionary<string, string>
        {
            ["regionId"] = parentRegionId?.Trim() ?? string.Empty
        });

        var payload = UnwrapResponseData(response.Data);
        var results = new List<DirectoryNode>();

        foreach (var item in ReadArray(payload))
        {
            var id = ReadString(item, "id");
            if (string.IsNullOrWhiteSpace(id) || !visited.Add(id))
            {
                continue;
            }

            var name = PickText(ReadString(item, "name"), "未命名目录");
            var fullPath = string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath}/{name}";
            var hasChildren = ReadInt32(item, "hasChildren") == 1;
            var node = new DirectoryNode
            {
                Id = id,
                ParentId = string.IsNullOrWhiteSpace(parentRegionId) ? null : parentRegionId,
                Name = name,
                RegionCode = ReadString(item, "regionCode"),
                RegionGbId = ReadString(item, "regionGBId"),
                Level = ReadInt32(item, "level") ?? 0,
                HasChildren = hasChildren,
                HasDevice = ReadInt32(item, "havDevice") == 1,
                FullPath = fullPath,
                Children = hasChildren && recursive
                    ? LoadDirectoryChildren(id, fullPath, recursive, visited).ToList()
                    : []
            };

            results.Add(node);
        }

        return results
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<DeviceDirectoryItem> EnrichDevices(
        IReadOnlyList<DeviceDirectoryItem> items,
        string? directoryId,
        string? directoryName,
        string? directoryPath)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var cachedDevices = _cacheStore.LoadDevices()
            .ToDictionary(item => item.DeviceCode, TextComparer);
        var deviceCodes = items.Select(item => item.DeviceCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(TextComparer)
            .ToList();

        var statusLookup = LoadStatusInfo(deviceCodes);
        var connectLookup = LoadConnectInfo(deviceCodes);
        var enriched = new List<DeviceDirectoryItem>(items.Count);

        foreach (var item in items)
        {
            var detail = NeedsDetailLookup(item, cachedDevices)
                ? TryLoadDeviceInfo(item.DeviceCode)
                : null;

            var deviceSource = TryLoadDeviceSource(item.DeviceCode);
            var merged = ResolveDirectoryAssignment(MergeDeviceItem(
                cachedDevices.GetValueOrDefault(item.DeviceCode),
                item,
                statusLookup.GetValueOrDefault(item.DeviceCode),
                connectLookup.GetValueOrDefault(item.DeviceCode),
                deviceSource,
                directoryId,
                directoryName,
                directoryPath));

            if (detail is not null)
            {
                merged = ResolveDirectoryAssignment(MergeDeviceItem(
                    merged,
                    detail,
                    statusLookup.GetValueOrDefault(item.DeviceCode),
                    connectLookup.GetValueOrDefault(item.DeviceCode),
                    deviceSource,
                    directoryId,
                    directoryName,
                    directoryPath));
            }

            enriched.Add(merged);
        }

        _cacheStore.UpsertDevices(enriched);
        return enriched;
    }

    private DeviceDirectoryItem? TryLoadDeviceInfo(string deviceCode)
    {
        try
        {
            return CreateBaseDeviceItem(
                UnwrapResponseData(Execute(DeviceInfoEndpoint, new Dictionary<string, string>
                {
                    ["deviceCode"] = deviceCode
                }).Data),
                deviceCode);
        }
        catch
        {
            return null;
        }
    }

    private int? TryLoadDeviceSource(string deviceCode)
    {
        try
        {
            return LoadDeviceSource(deviceCode);
        }
        catch
        {
            return null;
        }
    }

    private int? LoadDeviceSource(string deviceCode)
    {
        var response = Execute(DeviceResourceEndpoint, new Dictionary<string, string>
        {
            ["deviceCode"] = deviceCode
        });

        return ReadInt32(UnwrapResponseData(response.Data), "deviceSource");
    }

    private Dictionary<string, int> LoadStatusInfo(IReadOnlyList<string> deviceCodes)
    {
        var lookup = new Dictionary<string, int>(TextComparer);
        if (deviceCodes.Count == 0)
        {
            return lookup;
        }

        foreach (var chunk in Chunk(deviceCodes, 100))
        {
            try
            {
                var response = Execute(DeviceStatusEndpoint, new Dictionary<string, string>
                {
                    ["deviceCodes"] = string.Join(",", chunk),
                    ["queryData"] = "1"
                });

                foreach (var item in ReadArray(UnwrapResponseData(response.Data)))
                {
                    var code = ReadString(item, "deviceCode");
                    var status = ReadInt32(item, "status");
                    if (!string.IsNullOrWhiteSpace(code) && status.HasValue)
                    {
                        lookup[code] = status.Value;
                    }
                }
            }
            catch
            {
                // Keep list rendering available even when status enrichment is unavailable.
            }
        }

        return lookup;
    }

    private Dictionary<string, DeviceConnectInfo> LoadConnectInfo(IReadOnlyList<string> deviceCodes)
    {
        var lookup = new Dictionary<string, DeviceConnectInfo>(TextComparer);
        if (deviceCodes.Count == 0)
        {
            return lookup;
        }

        foreach (var chunk in Chunk(deviceCodes, 10))
        {
            try
            {
                var response = Execute(DeviceConnectInfoEndpoint, new Dictionary<string, string>
                {
                    ["deviceCodes"] = string.Join(",", chunk)
                });

                foreach (var item in ReadArray(UnwrapResponseData(response.Data)))
                {
                    var code = ReadString(item, "deviceCode");
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    lookup[code] = new DeviceConnectInfo
                    {
                        DeviceCode = code,
                        NodeId = ReadString(item, "nodeId"),
                        RegionCode = ReadString(item, "regionCode"),
                        NetTypeCode = TryGetPropertyIgnoreCase(item, "netType", out var netType) ? ReadInt32(netType, "code") : null,
                        NetTypeText = TryGetPropertyIgnoreCase(item, "netType", out netType)
                            ? PickText(ReadString(netType, "desc"), "未知")
                            : "未知"
                    };
                }
            }
            catch
            {
                // Keep detail panel available even if connection lookup fails.
            }
        }

        return lookup;
    }

    private static DeviceDirectoryItem CreateBaseDeviceItem(JsonElement element, string? fallbackDeviceCode = null)
    {
        var deviceCode = PickText(ReadString(element, "deviceCode"), fallbackDeviceCode ?? string.Empty);
        return new DeviceDirectoryItem
        {
            DeviceCode = deviceCode,
            DeviceName = PickText(ReadString(element, "deviceName"), "未命名设备"),
            Longitude = ReadString(element, "longitude"),
            Latitude = ReadString(element, "latitude"),
            RegionCode = ReadString(element, "regionCode", "location"),
            RegionGbId = ReadString(element, "regionGBId"),
            GbId = ReadString(element, "gbId"),
            SourceGbId = ReadString(element, "sourceGbId"),
            LastSyncedAt = DateTimeOffset.Now
        };
    }

    private static DevicePathInfo MapPathInfo(JsonElement element, string deviceCode)
    {
        return new DevicePathInfo
        {
            DeviceCode = deviceCode,
            DeviceFullPath = ReadString(element, "deviceFullPath"),
            BizFullPath = ReadString(element, "bizFullPath"),
            ProvincialIndustryFullPaths = ReadArray(element, "provincialIndustryFullPath")
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList(),
            LastSyncedAt = DateTimeOffset.Now
        };
    }

    private static DevicePathInfo CreateEmptyPath(string deviceCode)
    {
        return new DevicePathInfo
        {
            DeviceCode = deviceCode,
            LastSyncedAt = DateTimeOffset.Now
        };
    }

    private static DeviceDirectoryItem MergeDeviceItem(
        DeviceDirectoryItem? existing,
        DeviceDirectoryItem current,
        int? onlineStatus,
        DeviceConnectInfo? connectInfo,
        int? deviceSource,
        string? directoryId,
        string? directoryName,
        string? directoryPath)
    {
        return new DeviceDirectoryItem
        {
            DeviceCode = PickText(current.DeviceCode, existing?.DeviceCode ?? string.Empty),
            DeviceName = PickText(current.DeviceName, existing?.DeviceName ?? "未命名设备"),
            OnlineStatus = onlineStatus ?? current.OnlineStatus ?? existing?.OnlineStatus,
            OnlineStatusText = MapOnlineStatusText(onlineStatus ?? current.OnlineStatus ?? existing?.OnlineStatus),
            DirectoryId = PickText(directoryId, current.DirectoryId, existing?.DirectoryId ?? string.Empty),
            DirectoryName = PickText(directoryName, current.DirectoryName, existing?.DirectoryName ?? AllDevicesLabel),
            DirectoryPath = PickText(directoryPath, current.DirectoryPath, existing?.DirectoryPath ?? string.Empty),
            DeviceSource = deviceSource ?? current.DeviceSource ?? existing?.DeviceSource,
            DeviceSourceText = MapDeviceSourceText(deviceSource ?? current.DeviceSource ?? existing?.DeviceSource),
            Longitude = Coalesce(current.Longitude, existing?.Longitude),
            Latitude = Coalesce(current.Latitude, existing?.Latitude),
            RegionCode = Coalesce(connectInfo?.RegionCode, current.RegionCode, existing?.RegionCode),
            RegionGbId = Coalesce(current.RegionGbId, existing?.RegionGbId),
            GbId = Coalesce(current.GbId, existing?.GbId),
            SourceGbId = Coalesce(current.SourceGbId, existing?.SourceGbId),
            NodeId = Coalesce(connectInfo?.NodeId, current.NodeId, existing?.NodeId),
            NetTypeCode = connectInfo?.NetTypeCode ?? current.NetTypeCode ?? existing?.NetTypeCode,
            NetTypeText = PickText(connectInfo?.NetTypeText, current.NetTypeText, existing?.NetTypeText ?? "未知"),
            LastSyncedAt = DateTimeOffset.Now
        };
    }

    private static bool NeedsDetailLookup(
        DeviceDirectoryItem item,
        IReadOnlyDictionary<string, DeviceDirectoryItem> cachedDevices)
    {
        if (!cachedDevices.TryGetValue(item.DeviceCode, out var cached))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(cached.Longitude) ||
               string.IsNullOrWhiteSpace(cached.Latitude) ||
               string.IsNullOrWhiteSpace(cached.NodeId) ||
               string.IsNullOrWhiteSpace(cached.DirectoryName) ||
               cached.DeviceSource is null;
    }

    private string ResolveDirectoryName(string? regionId, string? regionName, string fallback = "目录设备")
    {
        if (!string.IsNullOrWhiteSpace(regionName))
        {
            return regionName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(regionId))
        {
            var node = FindDirectoryNode(_cacheStore.LoadDirectoryTree(), regionId.Trim());
            if (node is not null)
            {
                return node.Name;
            }
        }

        return fallback;
    }

    private string ResolveDirectoryPath(string? regionId, string? explicitPath, string directoryName)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(regionId))
        {
            var node = FindDirectoryNode(_cacheStore.LoadDirectoryTree(), regionId.Trim());
            if (node is not null)
            {
                return node.FullPath;
            }
        }

        return directoryName;
    }

    private static DirectoryNode? FindDirectoryNode(IEnumerable<DirectoryNode> nodes, string regionId)
    {
        foreach (var node in nodes)
        {
            if (TextComparer.Equals(node.Id, regionId))
            {
                return node;
            }

            var child = FindDirectoryNode(node.Children, regionId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static string MapOnlineStatusText(int? status)
    {
        return status switch
        {
            1 => "在线",
            0 => "离线",
            2 => "休眠",
            3 => "保活休眠",
            -1 => "不在账号下",
            _ => "未知"
        };
    }

    private static string MapDeviceSourceText(int? source)
    {
        return source switch
        {
            1 => "云眼",
            null => "未知",
            _ => "看家/其他"
        };
    }

    private static string Coalesce(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string PickText(string? primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary.Trim();
    }

    private static string PickText(string? primary, string? secondary, string fallback)
    {
        return !string.IsNullOrWhiteSpace(primary)
            ? primary.Trim()
            : !string.IsNullOrWhiteSpace(secondary)
                ? secondary.Trim()
                : fallback;
    }

    private DeviceDirectoryItem ResolveDirectoryAssignment(DeviceDirectoryItem item)
    {
        var hasExplicitDirectory = !string.IsNullOrWhiteSpace(item.DirectoryId) &&
                                   !TextComparer.Equals(item.DirectoryName, AllDevicesLabel) &&
                                   !string.IsNullOrWhiteSpace(item.DirectoryPath);
        if (hasExplicitDirectory)
        {
            return item;
        }

        var directoryNode = FindDirectoryNodeByRegion(_cacheStore.LoadDirectoryTree(), item.RegionCode, item.RegionGbId);
        if (directoryNode is null)
        {
            return item;
        }

        return new DeviceDirectoryItem
        {
            DeviceCode = item.DeviceCode,
            DeviceName = item.DeviceName,
            OnlineStatus = item.OnlineStatus,
            OnlineStatusText = item.OnlineStatusText,
            DirectoryId = directoryNode.Id,
            DirectoryName = directoryNode.Name,
            DirectoryPath = directoryNode.FullPath,
            DeviceSource = item.DeviceSource,
            DeviceSourceText = item.DeviceSourceText,
            Longitude = item.Longitude,
            Latitude = item.Latitude,
            RegionCode = item.RegionCode,
            RegionGbId = item.RegionGbId,
            GbId = item.GbId,
            SourceGbId = item.SourceGbId,
            NodeId = item.NodeId,
            NetTypeCode = item.NetTypeCode,
            NetTypeText = item.NetTypeText,
            LastSyncedAt = item.LastSyncedAt
        };
    }

    private static IEnumerable<IReadOnlyList<string>> Chunk(IReadOnlyList<string> values, int size)
    {
        for (var index = 0; index < values.Count; index += size)
        {
            yield return values.Skip(index).Take(size).ToList();
        }
    }

    private sealed class DeviceConnectInfo
    {
        public string DeviceCode { get; init; } = string.Empty;

        public string NodeId { get; init; } = string.Empty;

        public string RegionCode { get; init; } = string.Empty;

        public int? NetTypeCode { get; init; }

        public string NetTypeText { get; init; } = "未知";
    }

    private static DirectoryNode? FindDirectoryNodeByRegion(
        IEnumerable<DirectoryNode> nodes,
        string? regionCode,
        string? regionGbId)
    {
        foreach (var node in nodes)
        {
            if ((!string.IsNullOrWhiteSpace(regionCode) && TextComparer.Equals(node.RegionCode, regionCode)) ||
                (!string.IsNullOrWhiteSpace(regionGbId) && TextComparer.Equals(node.RegionGbId, regionGbId)))
            {
                return node;
            }

            var child = FindDirectoryNodeByRegion(node.Children, regionCode, regionGbId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }
}
