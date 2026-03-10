namespace TylinkInspection.Core.Models;

public sealed class PlatformConnectionTestResult
{
    public bool Success { get; init; }

    public bool ConfigurationValid { get; init; }

    public bool NetworkReady { get; init; }

    public bool TokenReady { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string AppId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string DecryptionMode { get; init; } = string.Empty;

    public OpenPlatformTokenState? TokenState { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
}
