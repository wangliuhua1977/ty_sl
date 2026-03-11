using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class LocalAiAlertService : IAiAlertService
{
    private readonly IAiAlertStore _alertStore;

    public LocalAiAlertService(IAiAlertStore alertStore)
    {
        _alertStore = alertStore;
        EnsureSeedData();
    }

    public ScrollQueryResult<AiAlertListItem> Query(AiAlertQuery query)
    {
        var filtered = ApplyScrollPagination(ApplyFilter(_alertStore.LoadAll(), query), query.LastSeenTime, query.LastSeenId)
            .OrderByDescending(item => item.CreateTime)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .Take(query.PageSize)
            .Select(MapToListItem)
            .ToList();

        var lastItem = filtered.LastOrDefault();
        return new ScrollQueryResult<AiAlertListItem>
        {
            Items = filtered,
            PageNo = query.PageNo,
            PageSize = query.PageSize,
            TotalCount = _alertStore.LoadAll().Count,
            HasMore = filtered.Count >= query.PageSize,
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

    private void EnsureSeedData()
    {
        if (_alertStore.LoadAll().Count > 0)
        {
            return;
        }

        _alertStore.SaveAll(
        [
            BuildAlert("AL-3001", "3001", 17, "火情识别", "TY-SH-0001", "虹桥枢纽 01", 3, "云测-AI能力中台", AiAlertWorkflowStatus.PendingConfirm, "检测到疑似火情，请尽快核验。", DateTimeOffset.Now.AddMinutes(-14), "未读"),
            BuildAlert("AL-3002", "3002", 15, "口罩识别", "TY-SZ-0017", "苏州园区 17", 2, "云化", AiAlertWorkflowStatus.Confirmed, "检测到口罩识别异常。", DateTimeOffset.Now.AddMinutes(-35), "已读"),
            BuildAlert("AL-3003", "3003", 3, "画面异常巡检", "TY-HZ-0008", "杭州西站 08", 1, "端侧", AiAlertWorkflowStatus.Dispatched, "AI 检测到持续黑屏。", DateTimeOffset.Now.AddHours(-1), "已读"),
            BuildAlert("AL-3004", "3004", 5, "区域入侵", "TY-NB-0012", "宁波港区", 4, "平安慧眼", AiAlertWorkflowStatus.Ignored, "非工作时段区域入侵告警。", DateTimeOffset.Now.AddHours(-2), "免打扰"),
            BuildAlert("AL-3005", "3005", 22, "电动车识别", "TY-HF-0022", "合肥南广场", 3, "云测-AI能力中台", AiAlertWorkflowStatus.Recovered, "电动车识别告警已恢复。", DateTimeOffset.Now.AddHours(-3), "已读")
        ]);
    }

    private static IEnumerable<AiAlertDetail> ApplyFilter(IReadOnlyList<AiAlertDetail> items, AiAlertQuery query)
    {
        var filtered = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.DeviceCode))
        {
            filtered = filtered.Where(item => item.DeviceCode.Contains(query.DeviceCode, StringComparison.OrdinalIgnoreCase));
        }

        if (query.AlertTypes.Count > 0)
        {
            filtered = filtered.Where(item => query.AlertTypes.Contains(item.AlertType));
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

        return filtered;
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

    private static AiAlertDetail BuildAlert(
        string id,
        string msgId,
        int alertType,
        string alertTypeName,
        string deviceCode,
        string deviceName,
        int alertSource,
        string alertSourceName,
        string workflowStatus,
        string content,
        DateTimeOffset createTime,
        string platformStatusText)
    {
        return new AiAlertDetail
        {
            Id = id,
            MsgId = msgId,
            PlatformAlertId = id,
            AlertType = alertType,
            AlertTypeName = alertTypeName,
            DeviceCode = deviceCode,
            DeviceName = deviceName,
            AlertSource = alertSource,
            AlertSourceName = alertSourceName,
            PlatformStatus = platformStatusText == "免打扰" ? 3 : 1,
            PlatformStatusText = platformStatusText,
            PlatformMessageRequestNo = long.TryParse(msgId, out var requestNo) ? requestNo : null,
            FeatureId = null,
            WorkflowStatus = workflowStatus,
            Content = content,
            Summary = content,
            CreateTime = createTime,
            UpdateTime = createTime.AddMinutes(2),
            SnapshotImageUrl = $"mock://snapshot/{id}",
            ThumbnailImageUrl = $"mock://thumbnail/{id}",
            BackgroundImageUrl = $"mock://background/{id}",
            DownloadUrl = $"mock://download/{id}",
            DownloadToken = $"mock-token-{id}",
            DownloadUrlExpireAt = createTime.AddDays(1),
            DownloadUrlRefreshStrategy = "下载地址过期后，可重新查询详情刷新。",
            CloudFileId = $"cloud-{id}",
            CloudFileName = $"{deviceCode}_{createTime:yyyyMMddHHmmss}.mp4",
            CloudFileIconUrl = $"mock://cloud-icon/{id}",
            WebUrl = $"mock://web/{id}",
            ReviewNote = null,
            Similarity = null,
            CarNumber = null,
            UserName = null,
            Remark = null
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
            _ => "TonePrimaryBrush"
        };
    }
}
