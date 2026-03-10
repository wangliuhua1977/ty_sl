using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;
using TylinkInspection.Infrastructure.OpenPlatform;

namespace TylinkInspection.Services;

public sealed class TokenService : ITokenService
{
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromHours(12);

    private readonly IOpenPlatformOptionsProvider _optionsProvider;
    private readonly ITokenCacheRepository _tokenCacheRepository;
    private readonly IOpenPlatformTokenClient _tokenClient;

    public TokenService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenCacheRepository tokenCacheRepository,
        IOpenPlatformTokenClient tokenClient)
    {
        _optionsProvider = optionsProvider;
        _tokenCacheRepository = tokenCacheRepository;
        _tokenClient = tokenClient;
    }

    public OpenPlatformTokenResult GetAvailableToken()
    {
        var options = _optionsProvider.GetOptions();
        var now = DateTimeOffset.UtcNow;
        var cache = LoadCache(options);

        if (cache is not null &&
            !string.IsNullOrWhiteSpace(cache.AccessToken) &&
            cache.ExpireAt is not null &&
            cache.ExpireAt.Value - now > RefreshThreshold)
        {
            return BuildTokenResult(cache, now);
        }

        if (cache is not null &&
            !string.IsNullOrWhiteSpace(cache.RefreshToken) &&
            cache.RefreshExpireAt is not null &&
            cache.RefreshExpireAt.Value > now)
        {
            try
            {
                var refreshedToken = _tokenClient.RefreshAccessToken(options, cache.RefreshToken);
                Persist(options, refreshedToken);
                return refreshedToken;
            }
            catch (OpenPlatformException)
            {
                var requestedToken = _tokenClient.RequestAccessToken(options);
                Persist(options, requestedToken);
                return requestedToken;
            }
        }

        var tokenResult = _tokenClient.RequestAccessToken(options);
        Persist(options, tokenResult);
        return tokenResult;
    }

    public OpenPlatformTokenResult RefreshToken()
    {
        var options = _optionsProvider.GetOptions();
        var now = DateTimeOffset.UtcNow;
        var cache = LoadCache(options);

        if (cache is null || string.IsNullOrWhiteSpace(cache.RefreshToken))
        {
            throw new OpenPlatformException("当前没有可用的 refreshToken，请先完成一次成功的令牌申请。", "missing_refresh_token");
        }

        if (cache.RefreshExpireAt is null || cache.RefreshExpireAt <= now)
        {
            throw new OpenPlatformException("当前 refreshToken 已失效，请重新申请 accessToken。", "refresh_token_expired");
        }

        var refreshedToken = _tokenClient.RefreshAccessToken(options, cache.RefreshToken);
        Persist(options, refreshedToken);
        return refreshedToken;
    }

    public OpenPlatformTokenState GetTokenState()
    {
        var options = _optionsProvider.GetOptions();
        var now = DateTimeOffset.UtcNow;
        var cache = LoadCache(options);

        if (cache is null || string.IsNullOrWhiteSpace(cache.AccessToken))
        {
            return new OpenPlatformTokenState
            {
                AppId = options.AppId,
                EnterpriseUser = options.EnterpriseUser,
                HasToken = false,
                CanReuseAccessToken = false,
                CanRefreshToken = false,
                Summary = "当前尚未缓存访问令牌。"
            };
        }

        var canReuse = cache.ExpireAt is not null && cache.ExpireAt.Value > now.Add(RefreshThreshold);
        var canRefresh = cache.RefreshExpireAt is not null && cache.RefreshExpireAt.Value > now;

        return new OpenPlatformTokenState
        {
            AppId = options.AppId,
            EnterpriseUser = options.EnterpriseUser,
            HasToken = true,
            CanReuseAccessToken = canReuse,
            CanRefreshToken = canRefresh,
            MaskedAccessToken = SensitiveDataMasker.MaskToken(cache.AccessToken),
            MaskedRefreshToken = SensitiveDataMasker.MaskToken(cache.RefreshToken),
            ExpireAt = cache.ExpireAt,
            RefreshExpireAt = cache.RefreshExpireAt,
            UpdatedAt = cache.UpdatedAt,
            Summary = canReuse
                ? "accessToken 可直接复用。"
                : canRefresh
                    ? "accessToken 接近过期，可使用 refreshToken 刷新。"
                    : "refreshToken 已失效，需要重新申请令牌。"
        };
    }

    private OpenPlatformTokenCache? LoadCache(TylinkOpenPlatformOptions options)
    {
        return _tokenCacheRepository.Load(options.AppId, options.EnterpriseUser);
    }

    private void Persist(TylinkOpenPlatformOptions options, OpenPlatformTokenResult tokenResult)
    {
        _tokenCacheRepository.Save(new OpenPlatformTokenCache
        {
            AppId = options.AppId,
            EnterpriseUser = options.EnterpriseUser,
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken,
            ExpireAt = tokenResult.ExpireAt,
            RefreshExpireAt = tokenResult.RefreshExpireAt,
            UpdatedAt = tokenResult.UpdatedAt
        });
    }

    private static OpenPlatformTokenResult BuildTokenResult(OpenPlatformTokenCache cache, DateTimeOffset now)
    {
        return new OpenPlatformTokenResult
        {
            AccessToken = cache.AccessToken ?? string.Empty,
            RefreshToken = cache.RefreshToken ?? string.Empty,
            ExpireAt = cache.ExpireAt ?? now,
            RefreshExpireAt = cache.RefreshExpireAt ?? cache.ExpireAt ?? now,
            UpdatedAt = cache.UpdatedAt ?? now
        };
    }
}
