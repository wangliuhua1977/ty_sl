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
            errors.Add("BaseUrl 不能为空。");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("BaseUrl 必须为 HTTPS 绝对地址。");
        }

        if (string.IsNullOrWhiteSpace(options.ApiVersion) || options.ApiVersion != OpenPlatformVersionPolicy.ApiVersion)
        {
            errors.Add("apiVersion 必须为 2.0。");
        }

        if (string.IsNullOrWhiteSpace(options.GrantType) || !string.Equals(options.GrantType, OpenPlatformTokenGrantTypes.UserUnaware, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("GrantType 仅支持 vcp_189，用于用户无感知获取令牌。");
        }

        if (string.Equals(options.ParentUser, "null", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("parentUser 不能传字符串 \"null\"。");
        }

        if (string.IsNullOrWhiteSpace(options.AppId))
        {
            errors.Add("AppId 不能为空。");
        }
        else if (string.Equals(options.AppId, "your-app-id", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("AppId 仍为模板占位值，请在 appsettings.Local.json 中填写真实值。");
        }

        if (string.IsNullOrWhiteSpace(options.AppSecret))
        {
            errors.Add("AppSecret 不能为空。");
        }
        else if (string.Equals(options.AppSecret, "your-app-secret", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("AppSecret 仍为模板占位值，请在 appsettings.Local.json 中填写真实值。");
        }

        if (string.IsNullOrWhiteSpace(options.ClientType))
        {
            errors.Add("ClientType 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            errors.Add("Version 不能为空。");
        }
        else if (OpenPlatformVersionPolicy.IsLegacyCompatibleVersion(options.Version) ||
                 OpenPlatformVersionPolicy.IsInvalidPrefixedCurrentVersion(options.Version))
        {
            errors.Add(OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage());
        }
        else if (!OpenPlatformVersionPolicy.IsCurrentVersion(options.Version))
        {
            errors.Add($"Version={options.Version} 无效。{OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage()}");
        }

        if (OpenPlatformVersionPolicy.IsCurrentVersion(options.Version) &&
            (string.IsNullOrWhiteSpace(options.RsaPrivateKey) || string.Equals(options.RsaPrivateKey, "your-rsa-private-key", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("Version=1.1 时必须提供 RSA 私钥，且不能保留模板占位值。");
        }

        if (string.IsNullOrWhiteSpace(options.EnterpriseUser))
        {
            errors.Add("EnterpriseUser 不能为空。");
        }
        else if (string.Equals(options.EnterpriseUser, "your-enterprise-user", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("EnterpriseUser 仍为模板占位值，请在 appsettings.Local.json 中填写真实值。");
        }

        return errors;
    }
}
