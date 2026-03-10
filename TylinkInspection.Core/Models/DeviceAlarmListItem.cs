namespace TylinkInspection.Core.Models;

public sealed class DeviceAlarmListItem
{
    public required string Id { get; init; }

    public string PlatformAlarmId { get; init; } = string.Empty;

    public required string DeviceCode { get; init; }

    public required string DeviceName { get; init; }

    public required int AlarmType { get; init; }

    public required string AlarmTypeName { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset CreateTime { get; init; }

    public DateTimeOffset? UpdateTime { get; init; }

    public int? PlatformStatus { get; init; }

    public string PlatformStatusText { get; init; } = "--";

    public string SeverityOrStatusText { get; init; } = "--";

    public string LocalStatus { get; init; } = "Synced";

    public string? ReviewNote { get; init; }

    public int? AlertSource { get; init; }

    public string AccentResourceKey { get; init; } = "ToneWarningBrush";
}
