using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;

namespace TylinkInspection.Infrastructure.OpenPlatform;

public sealed class OpenPlatformTokenClient : IOpenPlatformTokenClient
{
    private const string TokenEndpoint = "/open/oauth/getAccessToken";

    private readonly IOpenPlatformClient _openPlatformClient;

    public OpenPlatformTokenClient(IOpenPlatformClient openPlatformClient)
    {
        _openPlatformClient = openPlatformClient;
    }

    public OpenPlatformTokenResult RequestAccessToken(TylinkOpenPlatformOptions options)
    {
        ValidateRequest(options, options.GrantType, refreshToken: null);
        return Execute(options, options.GrantType, refreshToken: null);
    }

    public OpenPlatformTokenResult RefreshAccessToken(TylinkOpenPlatformOptions options, string refreshToken)
    {
        ValidateRequest(options, OpenPlatformTokenGrantTypes.RefreshToken, refreshToken);
        return Execute(options, OpenPlatformTokenGrantTypes.RefreshToken, refreshToken);
    }

    private OpenPlatformTokenResult Execute(TylinkOpenPlatformOptions options, string grantType, string? refreshToken)
    {
        var privateParameters = new Dictionary<string, string>
        {
            ["grantType"] = grantType
        };

        if (string.Equals(grantType, OpenPlatformTokenGrantTypes.RefreshToken, StringComparison.Ordinal))
        {
            privateParameters["refreshToken"] = refreshToken!;
        }

        try
        {
            var response = _openPlatformClient.Execute<OpenPlatformTokenResponseData>(TokenEndpoint, privateParameters, options);
            if (response.Data is null)
            {
                throw new OpenPlatformException("令牌接口返回成功，但 data 为空。", "empty_token_data");
            }

            var now = DateTimeOffset.UtcNow;
            return new OpenPlatformTokenResult
            {
                AccessToken = response.Data.AccessToken,
                RefreshToken = response.Data.RefreshToken,
                ExpireAt = now.AddSeconds(response.Data.ExpiresIn),
                RefreshExpireAt = now.AddSeconds(response.Data.RefreshExpiresIn),
                UpdatedAt = now
            };
        }
        catch (OpenPlatformException ex)
        {
            throw WrapException(ex, options, grantType);
        }
        catch (Exception ex)
        {
            throw WrapException(new OpenPlatformException("令牌请求失败。", "token_request_failed", ex), options, grantType);
        }
    }

    private static void ValidateRequest(TylinkOpenPlatformOptions options, string grantType, string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiVersion) || options.ApiVersion != OpenPlatformVersionPolicy.ApiVersion)
        {
            throw new OpenPlatformException("请求头 apiVersion 必须为 2.0。", "invalid_api_version");
        }

        if (!OpenPlatformVersionPolicy.IsCurrentVersion(options.Version))
        {
            throw new OpenPlatformException(OpenPlatformVersionPolicy.BuildCurrentVersionRequirementMessage(), "invalid_version");
        }

        if (string.Equals(options.ParentUser, "null", StringComparison.OrdinalIgnoreCase))
        {
            throw new OpenPlatformException("parentUser 不能传字符串 \"null\"。", "invalid_parent_user");
        }

        if (string.IsNullOrWhiteSpace(options.EnterpriseUser))
        {
            throw new OpenPlatformException("enterpriseUser 不能为空，用户无感知模式必须指定企业账号。", "missing_enterprise_user");
        }

        var isRefresh = string.Equals(grantType, OpenPlatformTokenGrantTypes.RefreshToken, StringComparison.Ordinal);
        var isUserUnaware = string.Equals(grantType, OpenPlatformTokenGrantTypes.UserUnaware, StringComparison.Ordinal);
        if (!isRefresh && !isUserUnaware)
        {
            throw new OpenPlatformException($"grantType={grantType} 无效，仅支持 vcp_189 或 refresh_token。", "invalid_grant_type");
        }

        if (isRefresh && string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new OpenPlatformException("refresh_token 模式必须传 refreshToken。", "missing_refresh_token");
        }

        if (!isRefresh && !string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new OpenPlatformException("非刷新场景不得传 refreshToken。", "unexpected_refresh_token");
        }

        if (!string.Equals(options.GrantType, OpenPlatformTokenGrantTypes.UserUnaware, StringComparison.OrdinalIgnoreCase))
        {
            throw new OpenPlatformException(
                $"当前应用配置的 GrantType={options.GrantType}，仅支持用户无感知获取令牌(vcp_189)。",
                "grant_type_mismatch");
        }
    }

    private static OpenPlatformException WrapException(OpenPlatformException exception, TylinkOpenPlatformOptions options, string grantType)
    {
        var maskedAppId = SensitiveDataMasker.MaskAppId(options.AppId);
        var maskedUser = SensitiveDataMasker.MaskEnterpriseUser(options.EnterpriseUser);
        var hint = string.Equals(grantType, OpenPlatformTokenGrantTypes.UserUnaware, StringComparison.Ordinal)
            ? "请确认开放平台应用授权方式已勾选“用户无感知获取令牌”。"
            : "请确认 refreshToken 仍在有效期内，且来自同一 AppId 与企业账号。";

        var message = $"{exception.Message} {hint} AppId={maskedAppId}, EnterpriseUser={maskedUser}";
        return new OpenPlatformException(message, exception.ErrorCode, exception);
    }
}
