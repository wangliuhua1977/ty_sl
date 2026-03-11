namespace TylinkInspection.Core.Models;

public sealed class PlaybackReviewPreparationRequest
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public int? NetTypeCode { get; init; }

    public DeviceInspectionResult? BaseInspectionResult { get; init; }

    public bool ForceRefresh { get; init; }
}
