using System.Text.Json.Serialization;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Core.Models;

public sealed class PlaybackReviewSource
{
    public string Protocol { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public DateTimeOffset? ExpireTime { get; init; }

    public string SourceCategory { get; init; } = string.Empty;

    public bool IsFallback { get; init; }

    [JsonIgnore]
    public string DisplayUrl => SensitiveDataMasker.MaskUrl(Url);

    [JsonIgnore]
    public string ExpireTimeText => ExpireTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
}
