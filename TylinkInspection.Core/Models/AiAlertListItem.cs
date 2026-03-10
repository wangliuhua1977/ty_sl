namespace TylinkInspection.Core.Models;

public sealed class AiAlertListItem
{
    public required string Id { get; init; }

    public required string MsgId { get; init; }

    public required string DeviceCode { get; init; }

    public required string DeviceName { get; init; }

    public required int AlertType { get; init; }

    public required string AlertTypeName { get; init; }

    public required int AlertSource { get; init; }

    public required string AlertSourceName { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreateTime { get; init; }

    public DateTimeOffset? UpdateTime { get; init; }

    public int? PlatformStatus { get; init; }

    public string PlatformStatusText { get; init; } = "--";

    public string Summary { get; init; } = string.Empty;

    public required string WorkflowStatus { get; init; }

    public string AccentResourceKey { get; init; } = "ToneWarningBrush";
}
