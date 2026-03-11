using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class OpenPlatformAiAlertService : OpenPlatformAlarmServiceBase, IAiAlertService
{
    private const string ListEndpoint = "/open/token/AIAlarm/getAlertInfoList";
    private const int DefaultAlertType = 3;

    private readonly IAiAlertStore _alertStore;

    public OpenPlatformAiAlertService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        IOpenPlatformClient openPlatformClient,
        IAiAlertStore alertStore)
        : base(optionsProvider, tokenService, openPlatformClient)
    {
        _alertStore = alertStore;
    }

    public ScrollQueryResult<AiAlertListItem> Query(AiAlertQuery query)
    {
        var response = Execute(ListEndpoint, BuildListParameters(query));
        var dataRoot = UnwrapResponseRoot(response.Data);
        var items = ReadArray(dataRoot, "list", "List");

        if (items.Count == 0 && dataRoot.ValueKind == JsonValueKind.Array)
        {
            items = dataRoot.EnumerateArray().ToList();
        }

        var remoteDetails = items
            .Select(MapToDetail)
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        MergeIntoStore(remoteDetails);

        var storeItems = _alertStore.LoadAll()
            .Where(item => remoteDetails.Any(remote => string.Equals(remote.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.CreateTime)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .ToList();

        var visibleItems = ApplyScrollPagination(storeItems, query.LastSeenTime, query.LastSeenId)
            .Select(MapToListItem)
            .ToList();

        var lastItem = visibleItems.LastOrDefault();
        var pageNo = ReadInt32(dataRoot, "pageNo") ?? query.PageNo;
        var pageSize = ReadInt32(dataRoot, "pageSize") ?? query.PageSize;
        var totalCount = ReadInt32(dataRoot, "total", "totalCount");
        var hasMore = totalCount.HasValue
            ? pageNo * pageSize < totalCount.Value
            : remoteDetails.Count >= query.PageSize;

        return new ScrollQueryResult<AiAlertListItem>
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

    public AiAlertDetail? GetDetail(string id)
    {
        return _alertStore.LoadAll().FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateWorkflowStatus(string id, string workflowStatus, string? reviewNote)
    {
        var alerts = _alertStore.LoadAll().ToList();
        var index = alerts.FindIndex(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var current = alerts[index];
        alerts[index] = new AiAlertDetail
        {
            Id = current.Id,
            MsgId = current.MsgId,
            PlatformAlertId = current.PlatformAlertId,
            AlertType = current.AlertType,
            AlertTypeName = current.AlertTypeName,
            DeviceCode = current.DeviceCode,
            DeviceName = current.DeviceName,
            AlertSource = current.AlertSource,
            AlertSourceName = current.AlertSourceName,
            PlatformStatus = current.PlatformStatus,
            PlatformStatusText = current.PlatformStatusText,
            PlatformMessageRequestNo = current.PlatformMessageRequestNo,
            FeatureId = current.FeatureId,
            WorkflowStatus = workflowStatus,
            Content = current.Content,
            Summary = current.Summary,
            CreateTime = current.CreateTime,
            UpdateTime = DateTimeOffset.Now,
            SnapshotImageUrl = current.SnapshotImageUrl,
            DownloadUrl = current.DownloadUrl,
            DownloadToken = current.DownloadToken,
            DownloadUrlExpireAt = current.DownloadUrlExpireAt,
            DownloadUrlRefreshStrategy = current.DownloadUrlRefreshStrategy,
            ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? current.ReviewNote : reviewNote.Trim()
        };

        _alertStore.SaveAll(alerts);
    }

    private IReadOnlyDictionary<string, string> BuildListParameters(AiAlertQuery query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pageNo"] = Math.Max(1, query.PageNo).ToString(),
            ["pageSize"] = Math.Max(1, query.PageSize).ToString()
        };
        var effectiveAlertTypes = query.AlertTypes.Count > 0
            ? query.AlertTypes
            : new[] { DefaultAlertType };

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

        if (effectiveAlertTypes.Count > 0)
        {
            parameters["alertTypeList"] = JoinCsv(effectiveAlertTypes);
        }

        if (query.AlertSource is not null)
        {
            parameters["alertSource"] = query.AlertSource.Value.ToString();
        }

        return parameters;
    }

    private static JsonElement UnwrapResponseRoot(JsonElement element)
    {
        var unwrapped = UnwrapResponseData(element);
        if (unwrapped.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(unwrapped, "data", out var nestedData) &&
            nestedData.ValueKind != JsonValueKind.Array)
        {
            return nestedData;
        }

        return unwrapped;
    }

    private void MergeIntoStore(IReadOnlyList<AiAlertDetail> remoteDetails)
    {
        var existingAlerts = _alertStore.LoadAll()
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var remoteDetail in remoteDetails)
        {
            if (existingAlerts.TryGetValue(remoteDetail.Id, out var current))
            {
                existingAlerts[remoteDetail.Id] = new AiAlertDetail
                {
                    Id = current.Id,
                    MsgId = remoteDetail.MsgId,
                    PlatformAlertId = remoteDetail.PlatformAlertId,
                    AlertType = remoteDetail.AlertType,
                    AlertTypeName = remoteDetail.AlertTypeName,
                    DeviceCode = remoteDetail.DeviceCode,
                    DeviceName = remoteDetail.DeviceName,
                    AlertSource = remoteDetail.AlertSource,
                    AlertSourceName = remoteDetail.AlertSourceName,
                    PlatformStatus = remoteDetail.PlatformStatus,
                    PlatformStatusText = remoteDetail.PlatformStatusText,
                    PlatformMessageRequestNo = remoteDetail.PlatformMessageRequestNo,
                    FeatureId = remoteDetail.FeatureId,
                    WorkflowStatus = string.IsNullOrWhiteSpace(current.WorkflowStatus)
                        ? remoteDetail.WorkflowStatus
                        : current.WorkflowStatus,
                    Content = remoteDetail.Content,
                    Summary = remoteDetail.Summary,
                    CreateTime = remoteDetail.CreateTime,
                    UpdateTime = remoteDetail.UpdateTime,
                    SnapshotImageUrl = current.SnapshotImageUrl,
                    DownloadUrl = current.DownloadUrl,
                    DownloadToken = current.DownloadToken,
                    DownloadUrlExpireAt = current.DownloadUrlExpireAt,
                    DownloadUrlRefreshStrategy = current.DownloadUrlRefreshStrategy ?? remoteDetail.DownloadUrlRefreshStrategy,
                    ReviewNote = current.ReviewNote
                };
            }
            else
            {
                existingAlerts[remoteDetail.Id] = remoteDetail;
            }
        }

        var ordered = existingAlerts.Values
            .OrderByDescending(item => item.CreateTime)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .ToList();

        _alertStore.SaveAll(ordered);
    }

    private static IEnumerable<AiAlertDetail> ApplyScrollPagination(IEnumerable<AiAlertDetail> items, DateTimeOffset? lastSeenTime, string? lastSeenId)
    {
        if (lastSeenTime is null || string.IsNullOrWhiteSpace(lastSeenId))
        {
            return items;
        }

        return items.Where(item =>
            item.CreateTime < lastSeenTime.Value ||
            (item.CreateTime == lastSeenTime.Value && string.CompareOrdinal(item.Id, lastSeenId) < 0));
    }

    private static AiAlertListItem MapToListItem(AiAlertDetail detail)
    {
        return new AiAlertListItem
        {
            Id = detail.Id,
            MsgId = detail.MsgId,
            DeviceCode = detail.DeviceCode,
            DeviceName = detail.DeviceName,
            AlertType = detail.AlertType,
            AlertTypeName = detail.AlertTypeName,
            AlertSource = detail.AlertSource,
            AlertSourceName = detail.AlertSourceName,
            Content = detail.Content,
            CreateTime = detail.CreateTime,
            UpdateTime = detail.UpdateTime,
            PlatformStatus = detail.PlatformStatus,
            PlatformStatusText = detail.PlatformStatusText,
            Summary = detail.Summary,
            WorkflowStatus = detail.WorkflowStatus,
            AccentResourceKey = MapAccent(detail.WorkflowStatus)
        };
    }

    private static AiAlertDetail MapToDetail(JsonElement item)
    {
        var platformAlertId = ReadString(item, "idStrExt", "id");
        var msgId = ReadString(item, "msgId", "msgReqNo", "id");
        var alertType = ReadInt32(item, "alertType") ?? 0;
        var alertSource = ReadInt32(item, "alertSource") ?? 0;
        var content = NormalizeText(ReadString(item, "content", "msgType"), "\u5e73\u53f0\u672a\u8fd4\u56de\u544a\u8b66\u6458\u8981");
        var createTime = ReadDateTimeOffset(item, "createTime", "alertTime") ?? DateTimeOffset.Now;

        return new AiAlertDetail
        {
            Id = NormalizeText(platformAlertId, msgId),
            MsgId = NormalizeText(msgId, platformAlertId),
            PlatformAlertId = platformAlertId,
            AlertType = alertType,
            AlertTypeName = ResolveAiAlertTypeName(alertType),
            DeviceCode = NormalizeText(ReadString(item, "deviceCode"), "--"),
            DeviceName = NormalizeText(ReadString(item, "deviceName"), "\u672a\u547d\u540d\u8bbe\u5907"),
            AlertSource = alertSource,
            AlertSourceName = ResolveAlertSourceName(alertSource),
            PlatformStatus = ReadInt32(item, "status"),
            PlatformStatusText = ResolveAiPlatformStatusText(ReadInt32(item, "status")),
            PlatformMessageRequestNo = ReadInt64(item, "msgReqNo", "msgId"),
            FeatureId = ReadInt32(item, "featureId"),
            WorkflowStatus = AiAlertWorkflowStatus.PendingConfirm,
            Content = content,
            Summary = content,
            CreateTime = createTime,
            UpdateTime = ReadDateTimeOffset(item, "updateTime"),
            SnapshotImageUrl = null,
            DownloadUrl = null,
            DownloadToken = null,
            DownloadUrlExpireAt = null,
            DownloadUrlRefreshStrategy = "\u540e\u7eed\u63a5\u5165\u771f\u5b9e\u8be6\u60c5\u63a5\u53e3\u540e\u5237\u65b0\u3002",
            ReviewNote = null
        };
    }

    private static string ResolveAiAlertTypeName(int alertType)
    {
        return alertType switch
        {
            3 => "\u753b\u9762\u5f02\u5e38\u5de1\u68c0",
            5 => "\u533a\u57df\u5165\u4fb5",
            12 => "\u5ba2\u6d41\u7edf\u8ba1",
            13 => "\u53a8\u5e3d\u8bc6\u522b",
            14 => "\u62bd\u70df\u8bc6\u522b",
            15 => "\u53e3\u7f69\u8bc6\u522b",
            16 => "\u73a9\u624b\u673a\u8bc6\u522b",
            17 => "\u706b\u60c5\u8bc6\u522b",
            18 => "\u4eba\u8138\u5e03\u63a7",
            19 => "\u8f66\u724c\u5e03\u63a7",
            20 => "\u5e73\u5b89\u6167\u773c\u533a\u57df\u5165\u4fb5",
            21 => "\u5927\u8c61\u8bc6\u522b",
            22 => "\u7535\u52a8\u8f66\u8bc6\u522b",
            23 => "\u6c34\u57df\u76d1\u63a7-\u533a\u57df\u5165\u4fb5",
            24 => "\u6c34\u57df\u76d1\u63a7-\u6ede\u7559\u544a\u8b66",
            25 => "\u4eba\u7fa4\u805a\u96c6\u68c0\u6d4b",
            26 => "\u533b\u7528\u9632\u62a4\u670d\u68c0\u6d4b",
            27 => "\u9ad8\u7a7a\u629b\u7269",
            _ => $"AI\u544a\u8b66\u7c7b\u578b {alertType}"
        };
    }

    private static string ResolveAlertSourceName(int alertSource)
    {
        return alertSource switch
        {
            1 => "\u7aef\u4fa7",
            2 => "\u4e91\u5316",
            3 => "\u4e91\u6d4b-AI\u80fd\u529b\u4e2d\u53f0",
            4 => "\u5e73\u5b89\u6167\u773c",
            _ => $"\u6765\u6e90 {alertSource}"
        };
    }

    private static string ResolveAiPlatformStatusText(int? status)
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

    private static string MapAccent(string workflowStatus)
    {
        return workflowStatus switch
        {
            AiAlertWorkflowStatus.PendingConfirm => "ToneWarningBrush",
            AiAlertWorkflowStatus.Confirmed => "TonePrimaryBrush",
            AiAlertWorkflowStatus.Ignored => "ToneFocusBrush",
            AiAlertWorkflowStatus.Dispatched => "ToneDangerBrush",
            AiAlertWorkflowStatus.Recovered => "ToneSuccessBrush",
            _ => "ToneWarningBrush"
        };
    }
}
