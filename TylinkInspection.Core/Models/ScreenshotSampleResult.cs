using System.Text.Json.Serialization;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Core.Models;

public sealed class ScreenshotSampleResult
{
    public string SampleId { get; init; } = Guid.NewGuid().ToString("N");

    public string ReviewSessionId { get; init; } = string.Empty;

    public string ReviewTargetKind { get; init; } = "Live";

    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string PlaybackFileName { get; init; } = string.Empty;

    public string Protocol { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; }

    public string ImagePath { get; init; } = string.Empty;

    [JsonIgnore]
    public string CapturedAtText => CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string SourceUrlText => SensitiveDataMasker.MaskUrl(SourceUrl);

    [JsonIgnore]
    public string ImageFileName => Path.GetFileName(ImagePath);

    [JsonIgnore]
    public bool HasImagePath => !string.IsNullOrWhiteSpace(ImagePath);

    [JsonIgnore]
    public string ReviewTargetText => string.Equals(ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase)
        ? "回看截图"
        : "直播截图";

    [JsonIgnore]
    public string ReviewSubjectText => string.Equals(ReviewTargetKind, "Playback", StringComparison.OrdinalIgnoreCase) &&
                                       !string.IsNullOrWhiteSpace(PlaybackFileName)
        ? PlaybackFileName
        : DeviceName;
}
