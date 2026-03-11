namespace TylinkInspection.Core.Models;

public sealed class PlaybackReviewPlaybackRequest
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string PlaybackFileId { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public string VideoEncoding { get; init; } = string.Empty;

    public string HlsStreamUrl { get; init; } = string.Empty;

    public string RtmpStreamUrl { get; init; } = string.Empty;

    public string DownloadUrl { get; init; } = string.Empty;

    public bool AddressRefreshed { get; init; }

    public string DiagnosticMessage { get; init; } = string.Empty;
}
