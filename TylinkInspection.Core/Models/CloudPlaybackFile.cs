using System.Text.Json.Serialization;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Core.Models;

public sealed class CloudPlaybackFile
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string IconUrl { get; init; } = string.Empty;

    public string FileType { get; init; } = string.Empty;

    public long Size { get; init; }

    public DateTimeOffset? CreateTime { get; init; }

    public DateTimeOffset? StreamResolvedAt { get; init; }

    public string? HlsStreamUrl { get; init; }

    public string? RtmpStreamUrl { get; init; }

    public string? DownloadUrl { get; init; }

    [JsonIgnore]
    public string CreateTimeText => CreateTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string IconUrlText => SensitiveDataMasker.MaskUrl(IconUrl);

    [JsonIgnore]
    public string StreamResolvedAtText => StreamResolvedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";

    [JsonIgnore]
    public string HlsStreamUrlText => SensitiveDataMasker.MaskUrl(HlsStreamUrl);

    [JsonIgnore]
    public string RtmpStreamUrlText => SensitiveDataMasker.MaskUrl(RtmpStreamUrl);

    [JsonIgnore]
    public string DownloadUrlText => SensitiveDataMasker.MaskUrl(DownloadUrl);

    [JsonIgnore]
    public bool HasStreamingAddress => !string.IsNullOrWhiteSpace(HlsStreamUrl) || !string.IsNullOrWhiteSpace(RtmpStreamUrl);
}
