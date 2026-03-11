namespace TylinkInspection.Core.Models;

public sealed class AiInspectionTaskExecutionRecord
{
    public string RecordId { get; init; } = Guid.NewGuid().ToString("N");

    public string TaskId { get; init; } = string.Empty;

    public string ItemId { get; init; } = string.Empty;

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string Message { get; init; } = string.Empty;

    public string AccentResourceKey { get; init; } = "TonePrimaryBrush";
}
