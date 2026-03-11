namespace TylinkInspection.Core.Models;

public sealed class ScreenshotSampleRequest
{
    public string ReviewSessionId { get; init; } = string.Empty;

    public string ReviewTargetKind { get; init; } = "Live";

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public string Protocol { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;

    public byte[] ImageBytes { get; init; } = Array.Empty<byte>();
}
