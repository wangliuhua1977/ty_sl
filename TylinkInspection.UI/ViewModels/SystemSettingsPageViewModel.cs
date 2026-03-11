using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Configuration;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;
using TylinkInspection.Core.Utilities;
using TylinkInspection.UI.Theming;

namespace TylinkInspection.UI.ViewModels;

public sealed class SystemSettingsPageViewModel : PageViewModelBase
{
    private readonly IOpenPlatformOptionsProvider _optionsProvider;
    private readonly IPlatformConnectionService _platformConnectionService;
    private readonly ITokenService _tokenService;
    private readonly IThemeService _themeService;
    private readonly IThemePreferenceStore _themePreferenceStore;

    private string _baseUrl = "--";
    private string _maskedAppId = "--";
    private string _maskedEnterpriseUser = "--";
    private string _maskedParentUser = "--";
    private string _grantType = "--";
    private string _clientType = "--";
    private string _version = "--";
    private string _apiVersion = "--";
    private string _decryptionMode = "--";
    private string _configurationRootPath = "--";
    private string _runtimeSettingsDirectoryPath = "--";
    private string _recommendedSettingsDirectoryPath = "--";
    private string _configurationSourceSummary = "--";
    private string _sharedSettingsPath = "--";
    private string _localSettingsPath = "--";
    private string _currentThemeDisplayName = "--";
    private string _themePreferenceStatusText = "\u5c1a\u672a\u521d\u59cb\u5316\u4e3b\u9898\u504f\u597d\u3002";
    private string _themePreferenceAccentResourceKey = "ToneInfoBrush";
    private string _tokenStatusSummary = "\u5c1a\u672a\u8bfb\u53d6 Token \u72b6\u6001\u3002";
    private string _maskedAccessToken = "--";
    private string _maskedRefreshToken = "--";
    private string _accessTokenExpireAtText = "--";
    private string _refreshTokenExpireAtText = "--";
    private string _updatedAtText = "--";
    private string _tokenStateAccentResourceKey = "ToneInfoBrush";
    private string _connectionSummary = "\u5c1a\u672a\u6267\u884c\u8fde\u63a5\u8bca\u65ad\u3002";
    private string _connectionErrorCode = "--";
    private string _connectionErrorMessage = "--";
    private string _connectionAccentResourceKey = "ToneInfoBrush";
    private bool _isBusy;

    public SystemSettingsPageViewModel(
        IOpenPlatformOptionsProvider optionsProvider,
        IPlatformConnectionService platformConnectionService,
        ITokenService tokenService,
        IThemeService themeService,
        IThemePreferenceStore themePreferenceStore)
        : base(
            "\u7cfb\u7edf\u8bbe\u7f6e",
            "\u5f53\u524d\u9875\u7528\u4e8e\u67e5\u770b\u5e73\u53f0\u914d\u7f6e\u3001Token \u72b6\u6001\u3001\u8fde\u63a5\u8bca\u65ad\u548c\u754c\u9762\u4e3b\u9898\u8bbe\u7f6e\u3002")
    {
        _optionsProvider = optionsProvider;
        _platformConnectionService = platformConnectionService;
        _tokenService = tokenService;
        _themeService = themeService;
        _themePreferenceStore = themePreferenceStore;

        ThemeOptions = new ObservableCollection<ThemeOptionViewModel>();
        DiagnosticMessages = new ObservableCollection<string>();

        SelectThemeCommand = new RelayCommand<ThemeOptionViewModel>(SelectTheme);
        TestConnectionCommand = new RelayCommand<object?>(_ => _ = TestConnectionAsync());
        RefreshTokenCommand = new RelayCommand<object?>(_ => _ = RefreshTokenAsync());

        InitializeThemeOptions();
        ReloadSnapshot();
    }

    public string BaseUrl
    {
        get => _baseUrl;
        private set => SetProperty(ref _baseUrl, value);
    }

    public string MaskedAppId
    {
        get => _maskedAppId;
        private set => SetProperty(ref _maskedAppId, value);
    }

    public string MaskedEnterpriseUser
    {
        get => _maskedEnterpriseUser;
        private set => SetProperty(ref _maskedEnterpriseUser, value);
    }

    public string MaskedParentUser
    {
        get => _maskedParentUser;
        private set => SetProperty(ref _maskedParentUser, value);
    }

    public string GrantType
    {
        get => _grantType;
        private set => SetProperty(ref _grantType, value);
    }

    public string ClientType
    {
        get => _clientType;
        private set => SetProperty(ref _clientType, value);
    }

    public string Version
    {
        get => _version;
        private set => SetProperty(ref _version, value);
    }

    public string ApiVersion
    {
        get => _apiVersion;
        private set => SetProperty(ref _apiVersion, value);
    }

    public string DecryptionMode
    {
        get => _decryptionMode;
        private set => SetProperty(ref _decryptionMode, value);
    }

    public string ConfigurationRootPath
    {
        get => _configurationRootPath;
        private set => SetProperty(ref _configurationRootPath, value);
    }

    public string RuntimeSettingsDirectoryPath
    {
        get => _runtimeSettingsDirectoryPath;
        private set => SetProperty(ref _runtimeSettingsDirectoryPath, value);
    }

    public string RecommendedSettingsDirectoryPath
    {
        get => _recommendedSettingsDirectoryPath;
        private set => SetProperty(ref _recommendedSettingsDirectoryPath, value);
    }

    public string ConfigurationSourceSummary
    {
        get => _configurationSourceSummary;
        private set => SetProperty(ref _configurationSourceSummary, value);
    }

    public string SharedSettingsPath
    {
        get => _sharedSettingsPath;
        private set => SetProperty(ref _sharedSettingsPath, value);
    }

    public string LocalSettingsPath
    {
        get => _localSettingsPath;
        private set => SetProperty(ref _localSettingsPath, value);
    }

    public string CurrentThemeDisplayName
    {
        get => _currentThemeDisplayName;
        private set => SetProperty(ref _currentThemeDisplayName, value);
    }

    public string ThemePreferenceStatusText
    {
        get => _themePreferenceStatusText;
        private set => SetProperty(ref _themePreferenceStatusText, value);
    }

    public string ThemePreferenceAccentResourceKey
    {
        get => _themePreferenceAccentResourceKey;
        private set => SetProperty(ref _themePreferenceAccentResourceKey, value);
    }

    public string TokenStatusSummary
    {
        get => _tokenStatusSummary;
        private set => SetProperty(ref _tokenStatusSummary, value);
    }

    public string MaskedAccessToken
    {
        get => _maskedAccessToken;
        private set => SetProperty(ref _maskedAccessToken, value);
    }

    public string MaskedRefreshToken
    {
        get => _maskedRefreshToken;
        private set => SetProperty(ref _maskedRefreshToken, value);
    }

    public string AccessTokenExpireAtText
    {
        get => _accessTokenExpireAtText;
        private set => SetProperty(ref _accessTokenExpireAtText, value);
    }

    public string RefreshTokenExpireAtText
    {
        get => _refreshTokenExpireAtText;
        private set => SetProperty(ref _refreshTokenExpireAtText, value);
    }

    public string UpdatedAtText
    {
        get => _updatedAtText;
        private set => SetProperty(ref _updatedAtText, value);
    }

    public string TokenStateAccentResourceKey
    {
        get => _tokenStateAccentResourceKey;
        private set => SetProperty(ref _tokenStateAccentResourceKey, value);
    }

    public string ConnectionSummary
    {
        get => _connectionSummary;
        private set => SetProperty(ref _connectionSummary, value);
    }

    public string ConnectionErrorCode
    {
        get => _connectionErrorCode;
        private set => SetProperty(ref _connectionErrorCode, value);
    }

    public string ConnectionErrorMessage
    {
        get => _connectionErrorMessage;
        private set => SetProperty(ref _connectionErrorMessage, value);
    }

    public string ConnectionAccentResourceKey
    {
        get => _connectionAccentResourceKey;
        private set => SetProperty(ref _connectionAccentResourceKey, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaisePropertyChanged(nameof(BusyText));
            }
        }
    }

    public string BusyText => IsBusy
        ? "\u6b63\u5728\u6267\u884c\u5e73\u53f0\u8bca\u65ad\uff0c\u8bf7\u7a0d\u5019..."
        : "\u53ef\u6267\u884c\u8fde\u63a5\u6d4b\u8bd5\u6216\u624b\u52a8\u5237\u65b0 Token\u3002";

    public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; }

    public ObservableCollection<string> DiagnosticMessages { get; }

    public ICommand SelectThemeCommand { get; }

    public ICommand TestConnectionCommand { get; }

    public ICommand RefreshTokenCommand { get; }

    private void InitializeThemeOptions()
    {
        ThemeOptions.Clear();
        foreach (var theme in _themeService.GetThemes())
        {
            ThemeOptions.Add(new ThemeOptionViewModel
            {
                Kind = theme.Kind,
                DisplayName = theme.DisplayName,
                Description = theme.Description,
                IsImplemented = theme.IsImplemented,
                IsSelected = theme.Kind == _themeService.CurrentTheme.Kind
            });
        }

        ApplyThemeSelectionState(_themeService.CurrentTheme.Kind);
        ApplyThemePreferenceHint(_themePreferenceStore.Load());
    }

    private void ReloadSnapshot()
    {
        var options = _optionsProvider.GetOptions();
        ApplyConfiguration(options);
        ApplyTokenState(_tokenService.GetTokenState());
        ApplyInitialHint(options);
    }

    private void SelectTheme(ThemeOptionViewModel? themeOption)
    {
        if (themeOption is null || !themeOption.IsImplemented)
        {
            return;
        }

        try
        {
            if (themeOption.Kind != _themeService.CurrentTheme.Kind)
            {
                _themeService.ApplyTheme(themeOption.Kind);
            }

            ApplyThemeSelectionState(themeOption.Kind);
        }
        catch (Exception ex)
        {
            ThemePreferenceStatusText = $"\u4e3b\u9898\u5207\u6362\u5931\u8d25\uff1a{ex.Message}";
            ThemePreferenceAccentResourceKey = "ToneDangerBrush";
            return;
        }

        try
        {
            _themePreferenceStore.Save(new ThemePreference
            {
                ThemeKey = ThemePreferenceMapper.ToStorageKey(themeOption.Kind),
                UpdatedAt = DateTimeOffset.UtcNow
            });

            ThemePreferenceStatusText = $"\u5df2\u5207\u6362\u4e3a\u300c{themeOption.DisplayName}\u300d\uff0c\u5e76\u5199\u5165\u672c\u5730\u4e3b\u9898\u504f\u597d\u3002";
            ThemePreferenceAccentResourceKey = "ToneSuccessBrush";
        }
        catch (Exception ex)
        {
            ThemePreferenceStatusText = $"\u5df2\u5207\u6362\u4e3a\u300c{themeOption.DisplayName}\u300d\uff0c\u4f46\u672c\u5730\u504f\u597d\u4fdd\u5b58\u5931\u8d25\uff1a{ex.Message}";
            ThemePreferenceAccentResourceKey = "ToneWarningBrush";
        }
    }

    private void ApplyThemeSelectionState(ThemeKind currentThemeKind)
    {
        foreach (var option in ThemeOptions)
        {
            option.IsSelected = option.Kind == currentThemeKind;
        }

        CurrentThemeDisplayName = _themeService.CurrentTheme.DisplayName;
    }

    private void ApplyThemePreferenceHint(ThemePreference? themePreference)
    {
        if (themePreference is null)
        {
            ThemePreferenceStatusText = "\u672c\u5730\u5c1a\u672a\u4fdd\u5b58\u4e3b\u9898\u504f\u597d\uff0c\u5f53\u524d\u5df2\u4f7f\u7528\u7535\u4fe1\u84dd\u767d\u7070\u9ed8\u8ba4\u4e3b\u9898\u3002";
            ThemePreferenceAccentResourceKey = "TonePrimaryBrush";
            return;
        }

        if (ThemePreferenceMapper.TryParseImplemented(_themeService, themePreference.ThemeKey, out var storedThemeKind) &&
            storedThemeKind == _themeService.CurrentTheme.Kind)
        {
            ThemePreferenceStatusText = $"\u5f53\u524d\u5df2\u6062\u590d\u4e0a\u6b21\u9009\u62e9\u7684\u4e3b\u9898\uff0c\u4fdd\u5b58\u65f6\u95f4 {FormatDateTime(themePreference.UpdatedAt)}\u3002";
            ThemePreferenceAccentResourceKey = "ToneSuccessBrush";
            return;
        }

        ThemePreferenceStatusText = "\u672c\u5730\u4e3b\u9898\u504f\u597d\u65e0\u6548\u6216\u5df2\u4e0d\u53ef\u7528\uff0c\u542f\u52a8\u65f6\u5df2\u56de\u9000\u5230\u7535\u4fe1\u84dd\u767d\u7070\u9ed8\u8ba4\u4e3b\u9898\u3002";
        ThemePreferenceAccentResourceKey = "ToneWarningBrush";
    }

    private async Task TestConnectionAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            ApplyConfiguration(_optionsProvider.GetOptions());
            var result = await Task.Run(() => _platformConnectionService.TestConnection());
            ApplyConnectionResult(result);
            ApplyTokenState(result.TokenState ?? _tokenService.GetTokenState());
        }
        catch (Exception ex)
        {
            ApplyConnectionFailure("unexpected_error", ex.Message);
            ApplyTokenState(_tokenService.GetTokenState());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshTokenAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            ApplyConfiguration(_optionsProvider.GetOptions());
            await Task.Run(() => _tokenService.RefreshToken());
            ApplyConnectionSuccess("\u5df2\u6309 refresh_token \u903b\u8f91\u5b8c\u6210\u624b\u52a8\u5237\u65b0\u3002");
            ApplyTokenState(_tokenService.GetTokenState());
        }
        catch (Exception ex)
        {
            ApplyConnectionFailure("refresh_failed", ex.Message);
            ApplyTokenState(_tokenService.GetTokenState());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyConfiguration(TylinkOpenPlatformOptions options)
    {
        BaseUrl = options.BaseUrl;
        MaskedAppId = SensitiveDataMasker.MaskAppId(options.AppId);
        MaskedEnterpriseUser = SensitiveDataMasker.MaskEnterpriseUser(options.EnterpriseUser);
        MaskedParentUser = string.IsNullOrWhiteSpace(options.ParentUser)
            ? "--"
            : SensitiveDataMasker.MaskEnterpriseUser(options.ParentUser);
        GrantType = options.GrantType;
        ClientType = options.ClientType;
        Version = options.Version;
        ApiVersion = options.ApiVersion;
        DecryptionMode = OpenPlatformVersionPolicy.ResolveDecryptionMode(options.Version);
        ConfigurationRootPath = string.IsNullOrWhiteSpace(options.ConfigurationRootPath) ? "--" : options.ConfigurationRootPath;
        RuntimeSettingsDirectoryPath = string.IsNullOrWhiteSpace(options.RuntimeSettingsDirectoryPath) ? "--" : options.RuntimeSettingsDirectoryPath;
        RecommendedSettingsDirectoryPath = string.IsNullOrWhiteSpace(options.RecommendedSettingsDirectoryPath) ? "--" : options.RecommendedSettingsDirectoryPath;
        ConfigurationSourceSummary = string.IsNullOrWhiteSpace(options.ConfigurationSourceSummary) ? "--" : options.ConfigurationSourceSummary;
        SharedSettingsPath = string.IsNullOrWhiteSpace(options.SharedSettingsPath)
            ? "appsettings.json not found"
            : options.SharedSettingsPath;
        LocalSettingsPath = string.IsNullOrWhiteSpace(options.LocalSettingsPath)
            ? "appsettings.Local.json \u672a\u627e\u5230\uff0c\u5f53\u524d\u4ec5\u4f7f\u7528 appsettings.json"
            : options.LocalSettingsPath;
    }

    private void ApplyInitialHint(TylinkOpenPlatformOptions options)
    {
        if (!HasTemplatePlaceholders(options))
        {
            return;
        }

        ConnectionSummary = "\u5f53\u524d\u4ecd\u4e3a\u6a21\u677f\u914d\u7f6e\uff0c\u8bf7\u5148\u5728 appsettings.Local.json \u4e2d\u586b\u5199\u771f\u5b9e\u5e73\u53f0\u53c2\u6570\u3002";
        ConnectionErrorCode = "local_config_required";
        ConnectionErrorMessage = "AppId / AppSecret / EnterpriseUser \u4ecd\u4e3a\u6a21\u677f\u5360\u4f4d\u503c\u3002";
        ConnectionAccentResourceKey = "ToneWarningBrush";
        DiagnosticMessages.Clear();
        DiagnosticMessages.Add("\u63a8\u8350\u5c06\u914d\u7f6e\u6587\u4ef6\u653e\u5728 TylinkInspection.App \u76ee\u5f55\uff0c\u6784\u5efa\u65f6\u4f1a\u81ea\u52a8\u590d\u5236\u5230\u8f93\u51fa\u76ee\u5f55\u3002");
        DiagnosticMessages.Add($"\u63a8\u8350\u7ef4\u62a4\u76ee\u5f55: {RecommendedSettingsDirectoryPath}");
        DiagnosticMessages.Add($"\u8fd0\u884c\u65f6\u8f93\u51fa\u76ee\u5f55: {RuntimeSettingsDirectoryPath}");
        DiagnosticMessages.Add($"\u5f53\u524d\u914d\u7f6e\u6765\u6e90: {ConfigurationSourceSummary}");
        DiagnosticMessages.Add("\u8bf7\u5728 appsettings.Local.json \u4e2d\u586b\u5199\u771f\u5b9e AppId\u3001AppSecret\u3001EnterpriseUser \u7b49\u654f\u611f\u503c\u3002");
    }

    private void ApplyTokenState(OpenPlatformTokenState tokenState)
    {
        TokenStatusSummary = tokenState.Summary;
        MaskedAccessToken = tokenState.MaskedAccessToken;
        MaskedRefreshToken = tokenState.MaskedRefreshToken;
        AccessTokenExpireAtText = FormatDateTime(tokenState.ExpireAt);
        RefreshTokenExpireAtText = FormatDateTime(tokenState.RefreshExpireAt);
        UpdatedAtText = FormatDateTime(tokenState.UpdatedAt);
        TokenStateAccentResourceKey = tokenState.HasToken
            ? tokenState.CanReuseAccessToken
                ? "ToneSuccessBrush"
                : tokenState.CanRefreshToken
                    ? "ToneWarningBrush"
                    : "ToneDangerBrush"
            : "ToneInfoBrush";
    }

    private void ApplyConnectionResult(PlatformConnectionTestResult result)
    {
        ConnectionSummary = result.Summary;
        ConnectionErrorCode = string.IsNullOrWhiteSpace(result.ErrorCode) ? "--" : result.ErrorCode!;
        ConnectionErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "--" : result.ErrorMessage!;
        ConnectionAccentResourceKey = result.Success ? "ToneSuccessBrush" : "ToneDangerBrush";

        DiagnosticMessages.Clear();
        foreach (var message in result.Messages)
        {
            DiagnosticMessages.Add(message);
        }
    }

    private void ApplyConnectionSuccess(string message)
    {
        ConnectionSummary = message;
        ConnectionErrorCode = "--";
        ConnectionErrorMessage = "--";
        ConnectionAccentResourceKey = "ToneSuccessBrush";
        DiagnosticMessages.Clear();
        DiagnosticMessages.Add(message);
        DiagnosticMessages.Add($"\u5f53\u524d\u914d\u7f6e\u6765\u6e90: {ConfigurationSourceSummary}");
    }

    private void ApplyConnectionFailure(string errorCode, string errorMessage)
    {
        ConnectionSummary = "\u64cd\u4f5c\u5931\u8d25\uff0c\u8bf7\u68c0\u67e5\u5e73\u53f0\u914d\u7f6e\u4e0e\u672c\u5730 Token \u72b6\u6001\u3002";
        ConnectionErrorCode = errorCode;
        ConnectionErrorMessage = errorMessage;
        ConnectionAccentResourceKey = "ToneDangerBrush";
        DiagnosticMessages.Clear();
        DiagnosticMessages.Add($"\u9519\u8bef\u7801: {errorCode}");
        DiagnosticMessages.Add($"\u9519\u8bef\u4fe1\u606f: {errorMessage}");
        DiagnosticMessages.Add($"\u5f53\u524d\u914d\u7f6e\u6765\u6e90: {ConfigurationSourceSummary}");
    }

    private static string FormatDateTime(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "--";
    }

    private static bool HasTemplatePlaceholders(TylinkOpenPlatformOptions options)
    {
        return string.Equals(options.AppId, "your-app-id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(options.AppSecret, "your-app-secret", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(options.EnterpriseUser, "your-enterprise-user", StringComparison.OrdinalIgnoreCase);
    }
}
