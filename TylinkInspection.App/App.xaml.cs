using System.Net.Http;
using System.Windows;
using TylinkInspection.Infrastructure.Configuration;
using TylinkInspection.Infrastructure.OpenPlatform;
using TylinkInspection.Infrastructure.Storage;
using TylinkInspection.Services;
using TylinkInspection.UI.Theming;
using TylinkInspection.UI.ViewModels;
using TylinkInspection.UI.Views;

namespace TylinkInspection.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var themeService = new ThemeService(Resources);
        themeService.ApplyTheme(ThemeKind.TechnologySituation);

        var optionsValidator = new OpenPlatformOptionsValidator();
        var optionsProvider = new JsonOpenPlatformOptionsProvider();
        var tokenCacheRepository = new JsonTokenCacheRepository();
        var aiTaskStore = new JsonAiInspectionTaskStore();
        var aiAlertStore = new JsonAiAlertStore();
        var deviceAlarmStore = new JsonDeviceAlarmStore();
        var paramEncryptor = new XxTeaOpenPlatformParamEncryptor();
        var signatureGenerator = new OpenPlatformSignatureGenerator();
        var requestBuilder = new OpenPlatformRequestBuilder(paramEncryptor, signatureGenerator, optionsValidator);
        var formSender = new OpenPlatformFormSender(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
        var responseDecryptor = new PlaceholderOpenPlatformResponseDecryptor();
        var openPlatformClient = new OpenPlatformClient(requestBuilder, formSender, responseDecryptor);
        var tokenClient = new OpenPlatformTokenClient(openPlatformClient);
        var tokenService = new TokenService(optionsProvider, tokenCacheRepository, tokenClient);
        var platformConnectionService = new PlatformConnectionService(optionsProvider, tokenService, optionsValidator);

        var workspaceService = new MockInspectionWorkspaceService();
        var aiInspectionCenterService = new AiInspectionCenterService(aiTaskStore);
        var aiAlertService = new OpenPlatformAiAlertService(optionsProvider, tokenService, openPlatformClient, aiAlertStore);
        var deviceAlarmService = new OpenPlatformDeviceAlarmService(optionsProvider, tokenService, openPlatformClient, deviceAlarmStore);
        var systemSettingsPageViewModel = new SystemSettingsPageViewModel(optionsProvider, platformConnectionService, tokenService);
        var shellWindow = new ShellWindow
        {
            DataContext = new MainShellViewModel(
                workspaceService,
                aiInspectionCenterService,
                aiAlertService,
                deviceAlarmService,
                systemSettingsPageViewModel)
        };

        MainWindow = shellWindow;
        shellWindow.Show();
    }
}
