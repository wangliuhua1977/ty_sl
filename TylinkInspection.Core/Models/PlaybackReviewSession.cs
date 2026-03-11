using System.Text.Json.Serialization;

namespace TylinkInspection.Core.Models;

public sealed class PlaybackReviewSession
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");

    public string ReviewTargetKind { get; init; } = "Live";

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string PlaybackFileId { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public string VideoEncoding { get; init; } = string.Empty;

    public string PreferredProtocol { get; init; } = string.Empty;

    public string FallbackProtocol { get; init; } = string.Empty;

    public string PreferredUrl { get; init; } = string.Empty;

    public string FallbackUrl { get; init; } = string.Empty;

    public DateTimeOffset? InspectionExpireTime { get; init; }

    public bool AddressRefreshed { get; init; }

    public string RefreshReason { get; init; } = string.Empty;

    public string DiagnosticMessage { get; init; } = string.Empty;

    public IReadOnlyList<PlaybackReviewSource> Sources { get; init; } = Array.Empty<PlaybackReviewSource>();

    [JsonIgnore]
    public bool HasSources => Sources.Count > 0;

    [JsonIgnore]
    public string InspectionExpireTimeText => InspectionExpireTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string ReviewTargetText => string.Equals(ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase)
        ? "回看复核"
        : "直播复核";

    [JsonIgnore]
    public string ReviewSubjectText => string.Equals(ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase) &&
                                       !string.IsNullOrWhiteSpace(PlaybackFileName)
        ? PlaybackFileName
        : DeviceName;
}
