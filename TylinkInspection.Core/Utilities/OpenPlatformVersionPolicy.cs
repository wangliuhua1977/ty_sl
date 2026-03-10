namespace TylinkInspection.Core.Utilities;

public static class OpenPlatformVersionPolicy
{
    public const string CurrentVersion = "1.1";
    public const string LegacyCompatibleVersion = "v1.0";
    public const string InvalidPrefixedCurrentVersion = "v1.1";
    public const string ApiVersion = "2.0";

    public static bool IsCurrentVersion(string? version)
    {
        return string.Equals(version, CurrentVersion, StringComparison.Ordinal);
    }

    public static bool IsLegacyCompatibleVersion(string? version)
    {
        return string.Equals(version, LegacyCompatibleVersion, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInvalidPrefixedCurrentVersion(string? version)
    {
        return string.Equals(version, InvalidPrefixedCurrentVersion, StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveDecryptionMode(string? version)
    {
        return version switch
        {
            CurrentVersion => "RSA",
            LegacyCompatibleVersion => "XXTea",
            _ => "Unknown"
        };
    }

    public static string BuildCurrentVersionRequirementMessage()
    {
        return "\u5f53\u524d\u9879\u76ee\u5e94\u6309\u65e7\u57fa\u7ebf\u4f7f\u7528 Version=1.1\uff0c\u4e0d\u5e94\u4f7f\u7528 v1.0 \u6216 v1.1\u3002";
    }
}
