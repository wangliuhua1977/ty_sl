namespace TylinkInspection.Core.Models;

public sealed class AiAlertDetail
{
    public required string Id { get; init; }

    public required string MsgId { get; init; }

    public string PlatformAlertId { get; init; } = string.Empty;

    public required int AlertType { get; init; }

    public required string AlertTypeName { get; init; }

    public required string DeviceCode { get; init; }

    public required string DeviceName { get; init; }

    public required int AlertSource { get; init; }

    public required string AlertSourceName { get; init; }

    public int? PlatformStatus { get; init; }

    public string PlatformStatusText { get; init; } = "--";

    public long? PlatformMessageRequestNo { get; init; }

    public int? FeatureId { get; init; }

    public required string WorkflowStatus { get; init; }

    public required string Content { get; init; }

    public string Summary { get; init; } = string.Empty;

    public required DateTimeOffset CreateTime { get; init; }

    public DateTimeOffset? UpdateTime { get; init; }

    public string? SnapshotImageUrl { get; init; }

    public string? DownloadUrl { get; init; }

    public string? DownloadToken { get; init; }

    public DateTimeOffset? DownloadUrlExpireAt { get; init; }

    public string? DownloadUrlRefreshStrategy { get; init; }

    public string? ReviewNote { get; init; }
}
