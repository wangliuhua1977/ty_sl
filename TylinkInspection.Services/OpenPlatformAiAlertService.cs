using System.Text.Json;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class OpenPlatformAiAlertService : OpenPlatformAlarmServiceBase, IAiAlertService
{
    private const string ListEndpoint = "/open/token/AIAlarm/getAlertInfoList";
    private const string DetailEndpoint = "/open/token/AIAlarm/getAlertInfoDetail";
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
            .Select(TryHydrateDetail)
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
            ThumbnailImageUrl = current.ThumbnailImageUrl,
            BackgroundImageUrl = current.BackgroundImageUrl,
            DownloadUrl = current.DownloadUrl,
            DownloadToken = current.DownloadToken,
            DownloadUrlExpireAt = current.DownloadUrlExpireAt,
            DownloadUrlRefreshStrategy = current.DownloadUrlRefreshStrategy,
            CloudFileId = current.CloudFileId,
            CloudFileName = current.CloudFileName,
            CloudFileIconUrl = current.CloudFileIconUrl,
            WebUrl = current.WebUrl,
            ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? current.ReviewNote : reviewNote.Trim(),
            Similarity = current.Similarity,
            CarNumber = current.CarNumber,
            UserName = current.UserName,
            Remark = current.Remark
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
                    SnapshotImageUrl = FirstNonEmpty(current.SnapshotImageUrl, remoteDetail.SnapshotImageUrl),
                    ThumbnailImageUrl = FirstNonEmpty(current.ThumbnailImageUrl, remoteDetail.ThumbnailImageUrl),
                    BackgroundImageUrl = FirstNonEmpty(current.BackgroundImageUrl, remoteDetail.BackgroundImageUrl),
                    DownloadUrl = FirstNonEmpty(current.DownloadUrl, remoteDetail.DownloadUrl),
                    DownloadToken = FirstNonEmpty(current.DownloadToken, remoteDetail.DownloadToken),
                    DownloadUrlExpireAt = current.DownloadUrlExpireAt ?? remoteDetail.DownloadUrlExpireAt,
                    DownloadUrlRefreshStrategy = FirstNonEmpty(current.DownloadUrlRefreshStrategy, remoteDetail.DownloadUrlRefreshStrategy),
                    CloudFileId = FirstNonEmpty(current.CloudFileId, remoteDetail.CloudFileId),
                    CloudFileName = FirstNonEmpty(current.CloudFileName, remoteDetail.CloudFileName),
                    CloudFileIconUrl = FirstNonEmpty(current.CloudFileIconUrl, remoteDetail.CloudFileIconUrl),
                    WebUrl = FirstNonEmpty(current.WebUrl, remoteDetail.WebUrl),
                    ReviewNote = current.ReviewNote,
                    Similarity = FirstNonEmpty(current.Similarity, remoteDetail.Similarity),
                    CarNumber = FirstNonEmpty(current.CarNumber, remoteDetail.CarNumber),
                    UserName = FirstNonEmpty(current.UserName, remoteDetail.UserName),
                    Remark = FirstNonEmpty(current.Remark, remoteDetail.Remark)
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
        var content = NormalizeText(ReadString(item, "content", "msgType"), "平台未返回告警摘要");
        var createTime = ReadDateTimeOffset(item, "createTime", "alertTime") ?? DateTimeOffset.Now;

        return new AiAlertDetail
        {
            Id = NormalizeText(platformAlertId, msgId),
            MsgId = NormalizeText(msgId, platformAlertId),
            PlatformAlertId = platformAlertId,
            AlertType = alertType,
            AlertTypeName = ResolveAiAlertTypeName(alertType),
            DeviceCode = NormalizeText(ReadString(item, "deviceCode"), "--"),
            DeviceName = NormalizeText(ReadString(item, "deviceName"), "未命名设备"),
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
            ThumbnailImageUrl = null,
            BackgroundImageUrl = null,
            DownloadUrl = null,
            DownloadToken = null,
            DownloadUrlExpireAt = null,
            DownloadUrlRefreshStrategy = "后续接入真实详情接口后刷新。",
            CloudFileId = null,
            CloudFileName = null,
            CloudFileIconUrl = null,
            WebUrl = null,
            ReviewNote = null,
            Similarity = null,
            CarNumber = null,
            UserName = null,
            Remark = null
        };
    }

    private AiAlertDetail TryHydrateDetail(AiAlertDetail summary)
    {
        if (string.IsNullOrWhiteSpace(summary.MsgId) ||
            string.IsNullOrWhiteSpace(summary.DeviceCode))
        {
            return summary;
        }

        try
        {
            var response = Execute(DetailEndpoint, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["msgId"] = summary.MsgId,
                ["alertType"] = summary.AlertType.ToString(),
                ["deviceCode"] = summary.DeviceCode
            });
            var detailRoot = UnwrapDetailRoot(response.Data);
            var paramsRoot = TryGetPropertyIgnoreCase(detailRoot, "params", out var paramsElement)
                ? paramsElement
                : detailRoot;

            return new AiAlertDetail
            {
                Id = summary.Id,
                MsgId = summary.MsgId,
                PlatformAlertId = summary.PlatformAlertId,
                AlertType = summary.AlertType,
                AlertTypeName = summary.AlertTypeName,
                DeviceCode = NormalizeText(ReadString(detailRoot, "deviceCode"), summary.DeviceCode),
                DeviceName = summary.DeviceName,
                AlertSource = summary.AlertSource,
                AlertSourceName = summary.AlertSourceName,
                PlatformStatus = summary.PlatformStatus,
                PlatformStatusText = summary.PlatformStatusText,
                PlatformMessageRequestNo = summary.PlatformMessageRequestNo,
                FeatureId = ReadInt32(detailRoot, "featureId") ?? ReadInt32(paramsRoot, "featureId") ?? summary.FeatureId,
                WorkflowStatus = summary.WorkflowStatus,
                Content = summary.Content,
                Summary = summary.Summary,
                CreateTime = summary.CreateTime,
                UpdateTime = summary.UpdateTime,
                SnapshotImageUrl = FirstNonEmpty(
                    ReadString(paramsRoot, "catchPatImageUrl"),
                    ReadString(paramsRoot, "imageUrl"),
                    ReadString(paramsRoot, "bgImageUrl"),
                    ReadString(paramsRoot, "cloudFileIconUrl")),
                ThumbnailImageUrl = FirstNonEmpty(
                    ReadString(paramsRoot, "imageUrl"),
                    ReadString(paramsRoot, "cloudFileIconUrl")),
                BackgroundImageUrl = ReadString(paramsRoot, "bgImageUrl"),
                DownloadUrl = FirstNonEmpty(
                    ReadString(paramsRoot, "cloudFileDownUrl"),
                    ReadString(paramsRoot, "webUrl")),
                DownloadToken = summary.DownloadToken,
                DownloadUrlExpireAt = summary.CreateTime.AddDays(1),
                DownloadUrlRefreshStrategy = "AI 告警详情证据图链接存在有效期，过期后重新查询详情刷新。",
                CloudFileId = ReadString(paramsRoot, "cloudFileId"),
                CloudFileName = ReadString(paramsRoot, "cloudFileName"),
                CloudFileIconUrl = ReadString(paramsRoot, "cloudFileIconUrl"),
                WebUrl = ReadString(paramsRoot, "webUrl"),
                ReviewNote = summary.ReviewNote,
                Similarity = ReadString(paramsRoot, "similarity"),
                CarNumber = ReadString(paramsRoot, "carNum"),
                UserName = ReadString(paramsRoot, "userName"),
                Remark = ReadString(paramsRoot, "remark")
            };
        }
        catch
        {
            return summary;
        }
    }

    private static JsonElement UnwrapDetailRoot(JsonElement element)
    {
        var unwrapped = UnwrapResponseData(element);
        if (unwrapped.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(unwrapped, "data", out var nestedData) &&
            nestedData.ValueKind == JsonValueKind.Object)
        {
            return nestedData;
        }

        return unwrapped;
    }

    private static string ResolveAiAlertTypeName(int alertType)
    {
        return alertType switch
        {
            3 => "画面异常巡检",
            5 => "区域入侵",
            12 => "客流统计",
            13 => "厨帽识别",
            14 => "抽烟识别",
            15 => "口罩识别",
            16 => "玩手机识别",
            17 => "火情识别",
            18 => "人脸布控",
            19 => "车牌布控",
            20 => "平安慧眼区域入侵",
            21 => "大象识别",
            22 => "电动车识别",
            23 => "水域监控-区域入侵",
            24 => "水域监控-滞留告警",
            25 => "人群聚集检测",
            26 => "医用防护服检测",
            27 => "高空抛物",
            _ => $"AI告警类型 {alertType}"
        };
    }

    private static string ResolveAlertSourceName(int alertSource)
    {
        return alertSource switch
        {
            1 => "端侧",
            2 => "云化",
            3 => "云测-AI能力中台",
            4 => "平安慧眼",
            _ => $"来源 {alertSource}"
        };
    }

    private static string ResolveAiPlatformStatusText(int? status)
    {
        return status switch
        {
            0 => "未读",
            1 => "已读",
            3 => "免打扰",
            null => "--",
            _ => $"状态 {status}"
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

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
