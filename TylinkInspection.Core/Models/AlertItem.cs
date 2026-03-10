namespace TylinkInspection.Core.Models;

public sealed class AlertItem
{
    public required string SiteName { get; init; }

    public required string Message { get; init; }

    public required string TimeText { get; init; }

    public required string SeverityLabel { get; init; }

    public required string AccentResourceKey { get; init; }
}
