namespace TylinkInspection.Core.Models;

public sealed class CloudPlaybackResolutionResult
{
    public string DeviceCode { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public DateTimeOffset ResolvedAt { get; init; } = DateTimeOffset.Now;

    public bool FromCache { get; init; }

    public CloudPlaybackFile File { get; init; } = new();

    public string DiagnosticMessage { get; init; } = string.Empty;
}
