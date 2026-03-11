namespace TylinkInspection.Core.Models;

public sealed class PlaybackReviewOutcome
{
    public string SessionId { get; init; } = string.Empty;

    public string ReviewTargetKind { get; init; } = "Live";

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string PlaybackFileId { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public DateTimeOffset ReviewedAt { get; init; } = DateTimeOffset.Now;

    public bool PlaybackStarted { get; init; }

    public bool FirstFrameVisible { get; init; }

    public int? StartupDurationMs { get; init; }

    public string UsedProtocol { get; init; } = string.Empty;

    public string UsedUrl { get; init; } = string.Empty;

    public bool UsedFallback { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public string VideoEncoding { get; init; } = string.Empty;
}
