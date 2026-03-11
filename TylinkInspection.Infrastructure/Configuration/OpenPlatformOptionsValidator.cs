using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Infrastructure.Configuration;

public sealed class OpenPlatformOptionsValidator
{
    public IReadOnlyList<string> Validate(TylinkOpenPlatformOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            errors.Add("BaseUrl \u4e0d\u80fd\u4e3a\u7a7a\u3002");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("BaseUrl \u5fc5\u987b\u4e3a HTTPS \u7edd\u5bf9\u5730\u5740\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.ApiVersion) || options.ApiVersion != OpenPlatformVersionPolicy.ApiVersion)
        {
            errors.Add("apiVersion \u5fc5\u987b\u4e3a 2.0\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.GrantType) || !string.Equals(options.GrantType, OpenPlatformTokenGrantTypes.UserUnaware, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("GrantType \u4ec5\u652f\u6301 vcp_189\uff0c\u7528\u4e8e\u7528\u6237\u65e0\u611f\u77e5\u83b7\u53d6\u4ee4\u724c\u3002");
        }

        if (string.Equals(options.ParentUser, "null", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("ParentUser \u4e0d\u80fd\u4f20\u5b57\u7b26\u4e32 \"null\"\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.AppId))
        {
            errors.Add("AppId \u4e0d\u80fd\u4e3a\u7a7a\u3002");
        }
        else if (string.Equals(options.AppId, "your-app-id", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("AppId \u4ecd\u4e3a\u6a21\u677f\u5360\u4f4d\u503c\uff0c\u8bf7\u5728 appsettings.Local.json \u4e2d\u586b\u5199\u771f\u5b9e\u503c\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.AppSecret))
        {
            errors.Add("AppSecret \u4e0d\u80fd\u4e3a\u7a7a\u3002");
        }
        else if (string.Equals(options.AppSecret, "your-app-secret", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("AppSecret \u4ecd\u4e3a\u6a21\u677f\u5360\u4f4d\u503c\uff0c\u8bf7\u5728 appsettings.Local.json \u4e2d\u586b\u5199\u771f\u5b9e\u503c\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.ClientType))
        {
            errors.Add("ClientType \u4e0d\u80fd\u4e3a\u7a7a\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            errors.Add("Version \u4e0d\u80fd\u4e3a\u7a7a\u3002");
        }
        else if (OpenPlatformVersionPolicy.IsLegacyCompatibleVersion(options.Version) ||
                 OpenPlatformVersionPolicy.IsInvalidPrefixedCurrentVersion(options.Version))
        {
            errors.Add(OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage());
        }
        else if (!OpenPlatformVersionPolicy.IsCurrentVersion(options.Version))
        {
            errors.Add($"Version={options.Version} \u65e0\u6548\u3002{OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage()}");
        }

        if (OpenPlatformVersionPolicy.IsCurrentVersion(options.Version) &&
            (string.IsNullOrWhiteSpace(options.RsaPrivateKey) || string.Equals(options.RsaPrivateKey, "your-rsa-private-key", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Version=1.1 \u65f6\u5fc5\u987b\u63d0\u4f9b RSA \u79c1\u94a5\uff0c\u4e14\u4e0d\u80fd\u4fdd\u7559\u6a21\u677f\u5360\u4f4d\u503c\u3002");
        }

        if (string.IsNullOrWhiteSpace(options.EnterpriseUser))
        {
            errors.Add("EnterpriseUser \u4e0d\u80fd\u4e3a\u7a7a\u3002");
        }
        else if (string.Equals(options.EnterpriseUser, "your-enterprise-user", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("EnterpriseUser \u4ecd\u4e3a\u6a21\u677f\u5360\u4f4d\u503c\uff0c\u8bf7\u5728 appsettings.Local.json \u4e2d\u586b\u5199\u771f\u5b9e\u503c\u3002");
        }

        return errors;
    }
}
