using System.Globalization;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class InspectionScopeService : IInspectionScopeService
{
    private const string CatalogTargetId = "ALL";
    private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IDeviceCatalogService _deviceCatalogService;
    private readonly IDeviceInspectionService _deviceInspectionService;
    private readonly IManualCoordinateService _manualCoordinateService;
    private readonly IInspectionScopeStore _store;
    private readonly object _syncRoot = new();

    private InspectionScopeState? _state;
    private InspectionScopeResult? _currentResult;
    private IReadOnlyList<DirectoryNode> _directoryTree = Array.Empty<DirectoryNode>();
    private IReadOnlyList<DeviceDirectoryItem> _devices = Array.Empty<DeviceDirectoryItem>();
    private bool _catalogLoaded;

    public InspectionScopeService(
        IDeviceCatalogService deviceCatalogService,
        IDeviceInspectionService deviceInspectionService,
        IManualCoordinateService manualCoordinateService,
        IInspectionScopeStore store)
    {
        _deviceCatalogService = deviceCatalogService;
        _deviceInspectionService = deviceInspectionService;
        _manualCoordinateService = manualCoordinateService;
        _store = store;
    }

    public event EventHandler? ScopeChanged;

    public IReadOnlyList<InspectionScopeScheme> GetSchemes()
    {
        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh: false);
            return _state!.Schemes.ToList();
        }
    }

    public InspectionScopeScheme GetCurrentScheme()
    {
        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh: false);
            return ResolveActiveScheme(_state!);
        }
    }

    public InspectionScopeResult GetCurrentScope()
    {
        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh: false);
            return _currentResult!;
        }
    }

    public InspectionScopeScheme SaveScheme(InspectionScopeScheme scheme)
    {
        InspectionScopeScheme savedScheme;

        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh: false);

            var now = DateTimeOffset.Now;
            var existing = _state!.Schemes.FirstOrDefault(item => TextComparer.Equals(item.Id, scheme.Id));
            savedScheme = SanitizeScheme(scheme, existing, now);

            var schemes = _state.Schemes
                .Where(item => !TextComparer.Equals(item.Id, savedScheme.Id))
                .ToList();
            schemes.Add(savedScheme);

            if (savedScheme.IsDefault)
            {
                schemes = schemes
                    .Select(item => item.Id == savedScheme.Id
                        ? item
                        : new InspectionScopeScheme
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Description = item.Description,
                            IsDefault = false,
                            Rules = item.Rules,
                            CreatedAt = item.CreatedAt,
                            UpdatedAt = item.UpdatedAt
                        })
                    .ToList();
            }

            _state = NormalizeState(new InspectionScopeState
            {
                Schemes = schemes,
                ActiveSchemeId = savedScheme.Id
            });

            PersistState();
            _currentResult = BuildResult();
            savedScheme = ResolveActiveScheme(_state);
        }

        RaiseScopeChanged();
        return savedScheme;
    }

    public void SetCurrentScheme(string schemeId)
    {
        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh: false);

            if (_state!.Schemes.All(item => !TextComparer.Equals(item.Id, schemeId)))
            {
                throw new InvalidOperationException("未找到巡检范围方案。");
            }

            _state = new InspectionScopeState
            {
                Schemes = _state.Schemes,
                ActiveSchemeId = schemeId
            };

            PersistState();
            _currentResult = BuildResult();
        }

        RaiseScopeChanged();
    }

    public void DeleteScheme(string schemeId)
    {
        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh: false);

            var existing = _state!.Schemes.FirstOrDefault(item => TextComparer.Equals(item.Id, schemeId));
            if (existing is null)
            {
                return;
            }

            if (existing.IsDefault)
            {
                throw new InvalidOperationException("默认方案不能删除，请先将其他方案设为默认方案。");
            }

            var schemes = _state.Schemes
                .Where(item => !TextComparer.Equals(item.Id, schemeId))
                .ToList();

            _state = NormalizeState(new InspectionScopeState
            {
                Schemes = schemes,
                ActiveSchemeId = TextComparer.Equals(_state.ActiveSchemeId, schemeId) ? null : _state.ActiveSchemeId
            });

            PersistState();
            _currentResult = BuildResult();
        }

        RaiseScopeChanged();
    }

    public void RefreshScope(bool forceCatalogRefresh = false)
    {
        lock (_syncRoot)
        {
            EnsureReady(forceCatalogRefresh);
            _currentResult = BuildResult();
        }

        RaiseScopeChanged();
    }

    private void EnsureReady(bool forceCatalogRefresh)
    {
        if (_state is null)
        {
            _state = NormalizeState(_store.Load());
        }

        if (!_catalogLoaded || forceCatalogRefresh)
        {
            LoadCatalog(forceCatalogRefresh);
        }

        if (_currentResult is null || forceCatalogRefresh)
        {
            _currentResult = BuildResult();
        }
    }

    private void LoadCatalog(bool forceRefresh)
    {
        try
        {
            _directoryTree = _deviceCatalogService.GetDirectoryTree(new DirectoryQuery
            {
                Recursive = true,
                ForceRefresh = forceRefresh
            });
        }
        catch
        {
            _directoryTree = _deviceCatalogService.GetCachedDirectoryTree();
        }

        try
        {
            _devices = _deviceCatalogService.EnsureAllDevicesCached(forceRefresh);
        }
        catch
        {
            _devices = _deviceCatalogService.GetCachedDevices();
        }

        _catalogLoaded = true;
    }

    private InspectionScopeResult BuildResult()
    {
        var currentScheme = ResolveActiveScheme(_state!);
        var manualCoordinateLookup = _manualCoordinateService.GetAll()
            .Where(item => !string.IsNullOrWhiteSpace(item.DeviceCode))
            .ToDictionary(item => item.DeviceCode, TextComparer);
        var inspectionLookup = _deviceInspectionService.GetLatestResults(
            _devices.Select(item => item.DeviceCode));
        var deviceLookup = _devices
            .Where(item => !string.IsNullOrWhiteSpace(item.DeviceCode))
            .GroupBy(item => item.DeviceCode, TextComparer)
            .ToDictionary(group => group.Key, group => group.Last(), TextComparer);
        var descendants = BuildDirectoryDescendantMap(_directoryTree);

        var includedDeviceCodes = new HashSet<string>(TextComparer);
        var excludedDeviceCodes = new HashSet<string>(TextComparer);
        var focusedDeviceCodes = new HashSet<string>(TextComparer);

        foreach (var rule in currentScheme.Rules)
        {
            var targetCodes = ResolveRuleDeviceCodes(rule, deviceLookup, descendants);
            switch (rule.Action)
            {
                case InspectionScopeRuleAction.Include:
                    foreach (var code in targetCodes)
                    {
                        includedDeviceCodes.Add(code);
                    }

                    break;
                case InspectionScopeRuleAction.Exclude:
                    foreach (var code in targetCodes)
                    {
                        excludedDeviceCodes.Add(code);
                    }

                    break;
                case InspectionScopeRuleAction.Focus:
                    foreach (var code in targetCodes)
                    {
                        focusedDeviceCodes.Add(code);
                    }

                    break;
            }
        }

        var scopeDevices = includedDeviceCodes
            .Where(code => !excludedDeviceCodes.Contains(code))
            .Select(code => deviceLookup.GetValueOrDefault(code))
            .Where(device => device is not null)
            .Select(device => device!)
            .OrderByDescending(device => focusedDeviceCodes.Contains(device.DeviceCode))
            .ThenByDescending(device => device.OnlineStatus == 1)
            .ThenBy(device => device.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.DeviceCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scopeDeviceModels = scopeDevices
            .Select(device =>
            {
                var latestInspection = inspectionLookup.GetValueOrDefault(device.DeviceCode);
                var effectiveDevice = ApplyInspectionOverlay(device, latestInspection);
                var hasCoordinate = TryResolveCoordinate(device, manualCoordinateLookup, out var longitude, out var latitude, out var coordinateSource);
                return new InspectionScopeDevice
                {
                    Device = effectiveDevice,
                    IsInCurrentScope = true,
                    IsFocused = focusedDeviceCodes.Contains(device.DeviceCode),
                    IsOnline = effectiveDevice.OnlineStatus == 1,
                    HasCoordinate = hasCoordinate,
                    Longitude = hasCoordinate ? longitude : null,
                    Latitude = hasCoordinate ? latitude : null,
                    CoordinateSource = coordinateSource,
                    CoordinateSystem = "GCJ-02",
                    PlaybackHealthGrade = latestInspection?.PlaybackHealthGrade,
                    LastInspectionTime = latestInspection?.InspectionTime,
                    PreferredProtocol = latestInspection?.PreferredProtocol ?? string.Empty,
                    FallbackProtocol = latestInspection?.FallbackProtocol ?? string.Empty,
                    ExpireTime = latestInspection?.ExpireTime,
                    VideoEnc = latestInspection?.VideoEnc ?? string.Empty,
                    FailureReason = latestInspection?.FailureReason ?? string.Empty,
                    Suggestion = latestInspection?.Suggestion ?? string.Empty,
                    NeedRecheck = latestInspection?.NeedRecheck == true,
                    LatestInspection = latestInspection
                };
            })
            .ToList();

        var mapPoints = scopeDeviceModels
            .Select(CreateMapPoint)
            .ToList();

        var summary = BuildSummary(currentScheme, scopeDeviceModels);

        return new InspectionScopeResult
        {
            CurrentScheme = currentScheme,
            Summary = summary,
            Devices = scopeDeviceModels,
            VisibleDirectoryNodes = BuildVisibleDirectoryTree(_directoryTree, scopeDeviceModels.Select(item => item.Device).ToList()),
            MapPoints = mapPoints,
            GeneratedAt = DateTimeOffset.Now
        };
    }

    private static InspectionScopeSummary BuildSummary(
        InspectionScopeScheme scheme,
        IReadOnlyList<InspectionScopeDevice> devices)
    {
        var onlineCount = devices.Count(item => item.IsOnline);
        var withCoordinateCount = devices.Count(item => item.HasCoordinate);
        var focusCount = devices.Count(item => item.IsFocused);

        return new InspectionScopeSummary
        {
            SchemeId = scheme.Id,
            SchemeName = scheme.Name,
            IsDefaultScheme = scheme.IsDefault,
            CoveredPointCount = devices.Count,
            OnlinePointCount = onlineCount,
            OfflinePointCount = devices.Count - onlineCount,
            WithCoordinatePointCount = withCoordinateCount,
            WithoutCoordinatePointCount = devices.Count - withCoordinateCount,
            FocusPointCount = focusCount
        };
    }

    private IReadOnlyCollection<string> ResolveRuleDeviceCodes(
        InspectionScopeRule rule,
        IReadOnlyDictionary<string, DeviceDirectoryItem> deviceLookup,
        IReadOnlyDictionary<string, IReadOnlySet<string>> descendants)
    {
        if (string.IsNullOrWhiteSpace(rule.TargetId) && rule.TargetType != InspectionScopeTargetType.Catalog)
        {
            return Array.Empty<string>();
        }

        return rule.TargetType switch
        {
            InspectionScopeTargetType.Catalog => deviceLookup.Keys.ToList(),
            InspectionScopeTargetType.Device => ResolveDeviceTarget(rule.TargetId, deviceLookup),
            InspectionScopeTargetType.Directory => ResolveDirectoryTarget(rule, deviceLookup.Values, descendants),
            _ => Array.Empty<string>()
        };
    }

    private static IReadOnlyCollection<string> ResolveDeviceTarget(
        string targetId,
        IReadOnlyDictionary<string, DeviceDirectoryItem> deviceLookup)
    {
        return deviceLookup.ContainsKey(targetId)
            ? [targetId]
            : Array.Empty<string>();
    }

    private static IReadOnlyCollection<string> ResolveDirectoryTarget(
        InspectionScopeRule rule,
        IEnumerable<DeviceDirectoryItem> devices,
        IReadOnlyDictionary<string, IReadOnlySet<string>> descendants)
    {
        HashSet<string>? targetDirectories = null;
        if (!string.IsNullOrWhiteSpace(rule.TargetId) && descendants.TryGetValue(rule.TargetId, out var directorySet))
        {
            targetDirectories = new HashSet<string>(directorySet, TextComparer);
        }

        return devices
            .Where(device =>
            {
                if (targetDirectories is not null && !string.IsNullOrWhiteSpace(device.DirectoryId))
                {
                    return targetDirectories.Contains(device.DirectoryId);
                }

                return !string.IsNullOrWhiteSpace(rule.TargetPath) &&
                       !string.IsNullOrWhiteSpace(device.DirectoryPath) &&
                       device.DirectoryPath.StartsWith(rule.TargetPath, StringComparison.OrdinalIgnoreCase);
            })
            .Select(device => device.DeviceCode)
            .Distinct(TextComparer)
            .ToList();
    }

    private static InspectionScopeMapPoint CreateMapPoint(InspectionScopeDevice scopeDevice)
    {
        return new InspectionScopeMapPoint
        {
            DeviceCode = scopeDevice.Device.DeviceCode,
            DeviceName = scopeDevice.Device.DeviceName,
            Longitude = scopeDevice.HasCoordinate ? scopeDevice.Longitude : null,
            Latitude = scopeDevice.HasCoordinate ? scopeDevice.Latitude : null,
            IsInCurrentScope = true,
            IsFocused = scopeDevice.IsFocused,
            IsOnline = scopeDevice.IsOnline,
            CoordinateSource = scopeDevice.CoordinateSource,
            CoordinateSystem = scopeDevice.CoordinateSystem,
            PlaybackHealthGrade = scopeDevice.PlaybackHealthGrade,
            NeedRecheck = scopeDevice.NeedRecheck,
            LastInspectionTime = scopeDevice.LastInspectionTime,
            FailureReasonSummary = scopeDevice.LatestInspection?.FailureReasonSummary ?? string.Empty
        };
    }

    private static DeviceDirectoryItem ApplyInspectionOverlay(DeviceDirectoryItem device, DeviceInspectionResult? latestInspection)
    {
        if (latestInspection is null)
        {
            return device;
        }

        return new DeviceDirectoryItem
        {
            DeviceCode = device.DeviceCode,
            DeviceName = string.IsNullOrWhiteSpace(latestInspection.DeviceName) ? device.DeviceName : latestInspection.DeviceName,
            OnlineStatus = latestInspection.OnlineStatus ?? device.OnlineStatus,
            OnlineStatusText = latestInspection.OnlineStatus.HasValue
                ? latestInspection.OnlineStatusText
                : device.OnlineStatusText,
            DirectoryId = device.DirectoryId,
            DirectoryName = device.DirectoryName,
            DirectoryPath = device.DirectoryPath,
            DeviceSource = device.DeviceSource,
            DeviceSourceText = device.DeviceSourceText,
            Longitude = device.Longitude,
            Latitude = device.Latitude,
            RegionCode = device.RegionCode,
            RegionGbId = device.RegionGbId,
            GbId = device.GbId,
            SourceGbId = device.SourceGbId,
            NodeId = device.NodeId,
            NetTypeCode = device.NetTypeCode,
            NetTypeText = device.NetTypeText,
            LastSyncedAt = device.LastSyncedAt
        };
    }

    private static bool TryResolveCoordinate(
        DeviceDirectoryItem device,
        IReadOnlyDictionary<string, ManualCoordinateRecord> manualCoordinateLookup,
        out double longitude,
        out double latitude,
        out InspectionScopeCoordinateSource coordinateSource)
    {
        if (!string.IsNullOrWhiteSpace(device.DeviceCode) &&
            manualCoordinateLookup.TryGetValue(device.DeviceCode, out var manualCoordinate))
        {
            longitude = manualCoordinate.Longitude;
            latitude = manualCoordinate.Latitude;
            coordinateSource = InspectionScopeCoordinateSource.Manual;
            return true;
        }

        var hasLongitude = double.TryParse(device.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
        var hasLatitude = double.TryParse(device.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude);
        coordinateSource = hasLongitude && hasLatitude
            ? InspectionScopeCoordinateSource.Platform
            : InspectionScopeCoordinateSource.Unknown;
        return hasLongitude && hasLatitude;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildDirectoryDescendantMap(
        IReadOnlyList<DirectoryNode> roots)
    {
        var map = new Dictionary<string, IReadOnlySet<string>>(TextComparer);
        foreach (var root in roots)
        {
            BuildDirectoryDescendants(root, map);
        }

        return map;
    }

    private static IReadOnlySet<string> BuildDirectoryDescendants(
        DirectoryNode node,
        IDictionary<string, IReadOnlySet<string>> map)
    {
        var values = new HashSet<string>(TextComparer) { node.Id };
        foreach (var child in node.Children)
        {
            values.UnionWith(BuildDirectoryDescendants(child, map));
        }

        map[node.Id] = values;
        return values;
    }

    private static IReadOnlyList<DirectoryNode> BuildVisibleDirectoryTree(
        IReadOnlyList<DirectoryNode> roots,
        IReadOnlyList<DeviceDirectoryItem> scopeDevices)
    {
        if (scopeDevices.Count == 0 || roots.Count == 0)
        {
            return [];
        }

        var parentLookup = new Dictionary<string, string?>(TextComparer);
        BuildParentLookup(roots, null, parentLookup);

        var visibleIds = new HashSet<string>(TextComparer);
        foreach (var device in scopeDevices)
        {
            if (string.IsNullOrWhiteSpace(device.DirectoryId))
            {
                continue;
            }

            var currentId = device.DirectoryId;
            while (!string.IsNullOrWhiteSpace(currentId) && visibleIds.Add(currentId) && parentLookup.TryGetValue(currentId, out var parentId))
            {
                currentId = parentId ?? string.Empty;
            }
        }

        return roots
            .Select(node => FilterDirectoryNode(node, visibleIds))
            .Where(node => node is not null)
            .Select(node => node!)
            .ToList();
    }

    private static void BuildParentLookup(
        IEnumerable<DirectoryNode> nodes,
        string? parentId,
        IDictionary<string, string?> lookup)
    {
        foreach (var node in nodes)
        {
            lookup[node.Id] = parentId;
            BuildParentLookup(node.Children, node.Id, lookup);
        }
    }

    private static DirectoryNode? FilterDirectoryNode(DirectoryNode node, IReadOnlySet<string> visibleIds)
    {
        var children = node.Children
            .Select(child => FilterDirectoryNode(child, visibleIds))
            .Where(child => child is not null)
            .Select(child => child!)
            .ToList();

        if (!visibleIds.Contains(node.Id) && children.Count == 0)
        {
            return null;
        }

        return new DirectoryNode
        {
            Id = node.Id,
            ParentId = node.ParentId,
            Name = node.Name,
            RegionCode = node.RegionCode,
            RegionGbId = node.RegionGbId,
            Level = node.Level,
            HasChildren = children.Count > 0,
            HasDevice = node.HasDevice,
            FullPath = node.FullPath,
            Children = children
        };
    }

    private static InspectionScopeScheme ResolveActiveScheme(InspectionScopeState state)
    {
        return state.Schemes.First(item => TextComparer.Equals(item.Id, state.ActiveSchemeId));
    }

    private InspectionScopeState NormalizeState(InspectionScopeState state)
    {
        var schemes = state.Schemes
            .Select((item, index) => NormalizeScheme(item, index))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        if (schemes.Count == 0)
        {
            schemes.Add(CreateDefaultScheme(DateTimeOffset.Now));
        }

        if (schemes.All(item => !item.IsDefault))
        {
            var firstScheme = schemes[0];
            schemes[0] = new InspectionScopeScheme
            {
                Id = firstScheme.Id,
                Name = firstScheme.Name,
                Description = firstScheme.Description,
                IsDefault = true,
                Rules = firstScheme.Rules,
                CreatedAt = firstScheme.CreatedAt,
                UpdatedAt = firstScheme.UpdatedAt
            };
        }

        var defaultSchemeId = schemes.First(item => item.IsDefault).Id;
        var activeSchemeId = schemes.Any(item => TextComparer.Equals(item.Id, state.ActiveSchemeId))
            ? state.ActiveSchemeId
            : defaultSchemeId;

        return new InspectionScopeState
        {
            Schemes = schemes
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ActiveSchemeId = activeSchemeId
        };
    }

    private static InspectionScopeScheme? NormalizeScheme(InspectionScopeScheme scheme, int index)
    {
        var id = string.IsNullOrWhiteSpace(scheme.Id)
            ? $"scope-{Guid.NewGuid():N}"
            : scheme.Id.Trim();
        var name = string.IsNullOrWhiteSpace(scheme.Name)
            ? $"巡检范围方案 {index + 1}"
            : scheme.Name.Trim();
        var rules = scheme.Rules
            .Where(rule => rule.TargetType == InspectionScopeTargetType.Catalog || !string.IsNullOrWhiteSpace(rule.TargetId))
            .Select(rule => new InspectionScopeRule
            {
                Action = rule.Action,
                TargetType = rule.TargetType,
                TargetId = rule.TargetType == InspectionScopeTargetType.Catalog ? CatalogTargetId : rule.TargetId.Trim(),
                TargetName = string.IsNullOrWhiteSpace(rule.TargetName) ? null : rule.TargetName.Trim(),
                TargetPath = string.IsNullOrWhiteSpace(rule.TargetPath) ? null : rule.TargetPath.Trim()
            })
            .Distinct(new InspectionScopeRuleComparer())
            .ToList();

        return new InspectionScopeScheme
        {
            Id = id,
            Name = name,
            Description = scheme.Description?.Trim() ?? string.Empty,
            IsDefault = scheme.IsDefault,
            Rules = rules,
            CreatedAt = scheme.CreatedAt == default ? DateTimeOffset.Now : scheme.CreatedAt,
            UpdatedAt = scheme.UpdatedAt == default ? DateTimeOffset.Now : scheme.UpdatedAt
        };
    }

    private static InspectionScopeScheme CreateDefaultScheme(DateTimeOffset now)
    {
        return new InspectionScopeScheme
        {
            Id = $"scope-default-{Guid.NewGuid():N}",
            Name = "默认巡检范围",
            Description = "默认覆盖当前缓存目录下的全部点位，可作为地图、点位治理和后续巡检编排的基础方案。",
            IsDefault = true,
            Rules =
            [
                new InspectionScopeRule
                {
                    Action = InspectionScopeRuleAction.Include,
                    TargetType = InspectionScopeTargetType.Catalog,
                    TargetId = CatalogTargetId,
                    TargetName = "全部缓存点位"
                }
            ],
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static InspectionScopeScheme SanitizeScheme(
        InspectionScopeScheme scheme,
        InspectionScopeScheme? existing,
        DateTimeOffset now)
    {
        var normalized = NormalizeScheme(new InspectionScopeScheme
        {
            Id = string.IsNullOrWhiteSpace(scheme.Id) ? $"scope-{Guid.NewGuid():N}" : scheme.Id.Trim(),
            Name = scheme.Name,
            Description = scheme.Description,
            IsDefault = scheme.IsDefault,
            Rules = scheme.Rules,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        }, index: 0)!;

        if (string.IsNullOrWhiteSpace(normalized.Name))
        {
            throw new InvalidOperationException("巡检范围方案名称不能为空。");
        }

        return normalized;
    }

    private void PersistState()
    {
        _store.Save(_state!);
    }

    private void RaiseScopeChanged()
    {
        ScopeChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class InspectionScopeRuleComparer : IEqualityComparer<InspectionScopeRule>
    {
        public bool Equals(InspectionScopeRule? x, InspectionScopeRule? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return x.Action == y.Action &&
                   x.TargetType == y.TargetType &&
                   TextComparer.Equals(x.TargetId, y.TargetId);
        }

        public int GetHashCode(InspectionScopeRule obj)
        {
            return HashCode.Combine(obj.Action, obj.TargetType, TextComparer.GetHashCode(obj.TargetId));
        }
    }
}
