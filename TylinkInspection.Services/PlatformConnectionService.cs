using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;
using TylinkInspection.Infrastructure.Configuration;
using TylinkInspection.Infrastructure.OpenPlatform;

namespace TylinkInspection.Services;

public sealed class PlatformConnectionService : IPlatformConnectionService
{
    private readonly IOpenPlatformOptionsProvider _optionsProvider;
    private readonly ITokenService _tokenService;
    private readonly OpenPlatformOptionsValidator _validator;

    public PlatformConnectionService(
        IOpenPlatformOptionsProvider optionsProvider,
        ITokenService tokenService,
        OpenPlatformOptionsValidator validator)
    {
        _optionsProvider = optionsProvider;
        _tokenService = tokenService;
        _validator = validator;
    }

    public PlatformConnectionTestResult TestConnection()
    {
        var options = _optionsProvider.GetOptions();
        var validationErrors = _validator.Validate(options).ToList();
        var messages = BuildDiagnosticMessages(options);
        var maskedAppId = SensitiveDataMasker.MaskAppId(options.AppId);
        var networkReady = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        var decryptionMode = OpenPlatformVersionPolicy.ResolveDecryptionMode(options.Version);

        if (validationErrors.Count > 0)
        {
            foreach (var validationError in validationErrors)
            {
                messages.Add(validationError);
            }

            return new PlatformConnectionTestResult
            {
                Success = false,
                ConfigurationValid = false,
                NetworkReady = networkReady,
                TokenReady = false,
                BaseUrl = options.BaseUrl,
                AppId = maskedAppId,
                Version = options.Version,
                DecryptionMode = decryptionMode,
                Summary = "\u5e73\u53f0\u914d\u7f6e\u672a\u901a\u8fc7\u6821\u9a8c\uff0c\u8bf7\u5148\u4fee\u6b63\u914d\u7f6e\u3002",
                ErrorCode = "invalid_configuration",
                ErrorMessage = string.Join(" ", validationErrors),
                Messages = messages
            };
        }

        try
        {
            _ = _tokenService.GetAvailableToken();
            var tokenState = _tokenService.GetTokenState();
            messages.Add("\u5df2\u6210\u529f\u8bfb\u53d6\u6216\u7533\u8bf7\u53ef\u7528 token\u3002");
            messages.Add(tokenState.Summary);

            return new PlatformConnectionTestResult
            {
                Success = true,
                ConfigurationValid = true,
                NetworkReady = networkReady,
                TokenReady = tokenState.HasToken,
                BaseUrl = options.BaseUrl,
                AppId = maskedAppId,
                Version = options.Version,
                DecryptionMode = decryptionMode,
                TokenState = tokenState,
                Summary = "\u8fde\u63a5\u6d4b\u8bd5\u6210\u529f\uff0c\u5e73\u53f0\u5df2\u53ef\u8bfb\u53d6\u6216\u7533\u8bf7\u53ef\u7528 token\u3002",
                Messages = messages
            };
        }
        catch (OpenPlatformException ex)
        {
            var tokenState = _tokenService.GetTokenState();
            messages.Add($"\u9519\u8bef\u7801: {ex.ErrorCode ?? "unknown"}");
            messages.Add($"\u9519\u8bef\u4fe1\u606f: {ex.Message}");

            return new PlatformConnectionTestResult
            {
                Success = false,
                ConfigurationValid = true,
                NetworkReady = networkReady,
                TokenReady = false,
                BaseUrl = options.BaseUrl,
                AppId = maskedAppId,
                Version = options.Version,
                DecryptionMode = decryptionMode,
                TokenState = tokenState,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message,
                Summary = "\u8fde\u63a5\u6d4b\u8bd5\u5931\u8d25\uff0c\u8bf7\u68c0\u67e5\u5f00\u653e\u5e73\u53f0\u914d\u7f6e\u4e0e\u6388\u6743\u65b9\u5f0f\u3002",
                Messages = messages
            };
        }
        catch (Exception ex)
        {
            var tokenState = _tokenService.GetTokenState();
            messages.Add("\u9519\u8bef\u7801: unexpected_error");
            messages.Add($"\u9519\u8bef\u4fe1\u606f: {ex.Message}");

            return new PlatformConnectionTestResult
            {
                Success = false,
                ConfigurationValid = true,
                NetworkReady = networkReady,
                TokenReady = false,
                BaseUrl = options.BaseUrl,
                AppId = maskedAppId,
                Version = options.Version,
                DecryptionMode = decryptionMode,
                TokenState = tokenState,
                ErrorCode = "unexpected_error",
                ErrorMessage = ex.Message,
                Summary = "\u8fde\u63a5\u6d4b\u8bd5\u51fa\u73b0\u672a\u9884\u671f\u5f02\u5e38\u3002",
                Messages = messages
            };
        }
    }

    private static List<string> BuildDiagnosticMessages(Core.Configuration.TylinkOpenPlatformOptions options)
    {
        var maskedAppId = SensitiveDataMasker.MaskAppId(options.AppId);
        var maskedUser = SensitiveDataMasker.MaskEnterpriseUser(options.EnterpriseUser);
        var decryptionMode = OpenPlatformVersionPolicy.ResolveDecryptionMode(options.Version);
        var localSettingsPath = string.IsNullOrWhiteSpace(options.LocalSettingsPath)
            ? "appsettings.Local.json \u672a\u627e\u5230\uff0c\u5f53\u524d\u4ec5\u4f7f\u7528 appsettings.json"
            : options.LocalSettingsPath;

        return
        [
            $"\u5f53\u524d AppId: {maskedAppId}",
            $"\u5f53\u524d EnterpriseUser: {maskedUser}",
            $"\u5f53\u524d Version: {options.Version}",
            $"\u5f53\u524d\u89e3\u5bc6\u6a21\u5f0f: {decryptionMode}",
            $"\u63a8\u8350\u7ef4\u62a4\u76ee\u5f55: {options.RecommendedSettingsDirectoryPath}",
            $"\u8fd0\u884c\u65f6\u8f93\u51fa\u76ee\u5f55: {options.RuntimeSettingsDirectoryPath}",
            $"\u5f53\u524d\u914d\u7f6e\u6765\u6e90: {options.ConfigurationSourceSummary}",
            $"\u4e3b\u914d\u7f6e\u76ee\u5f55: {options.ConfigurationRootPath}",
            $"\u5171\u4eab\u914d\u7f6e\u6587\u4ef6: {(string.IsNullOrWhiteSpace(options.SharedSettingsPath) ? "appsettings.json \u672a\u627e\u5230" : options.SharedSettingsPath)}",
            $"\u672c\u5730\u8986\u76d6\u6587\u4ef6: {localSettingsPath}"
        ];
    }
}
