namespace TylinkInspection.Core.Configuration;

public sealed class TylinkOpenPlatformOptions
{
    public string BaseUrl { get; init; } = "https://vcp.21cn.com";

    public string AppId { get; init; } = "your-app-id";

    public string AppSecret { get; init; } = "your-app-secret";

    public string RsaPrivateKey { get; init; } = "your-rsa-private-key";

    public string ClientType { get; init; } = "4";

    public string Version { get; init; } = TylinkInspection.Core.Utilities.OpenPlatformVersionPolicy.CurrentVersion;

    public string ApiVersion { get; init; } = TylinkInspection.Core.Utilities.OpenPlatformVersionPolicy.ApiVersion;

    public string GrantType { get; init; } = TylinkInspection.Core.Models.OpenPlatformTokenGrantTypes.UserUnaware;

    public string EnterpriseUser { get; init; } = "your-enterprise-user";

    public string? ParentUser { get; init; }

    public string ConfigurationRootPath { get; init; } = string.Empty;

    public string RuntimeSettingsDirectoryPath { get; init; } = string.Empty;

    public string RecommendedSettingsDirectoryPath { get; init; } = string.Empty;

    public string ConfigurationSourceSummary { get; init; } = string.Empty;

    public string SharedSettingsPath { get; init; } = string.Empty;

    public string? LocalSettingsPath { get; init; }

    public bool IsLocalSettingsLoaded { get; init; }
}
