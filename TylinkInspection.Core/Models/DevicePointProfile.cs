namespace TylinkInspection.Core.Models;

public sealed class DevicePointProfile
{
    public required DeviceDirectoryItem Device { get; init; }

    public required DevicePathInfo PathInfo { get; init; }
}
