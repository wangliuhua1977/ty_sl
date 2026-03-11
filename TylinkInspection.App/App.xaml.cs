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
        var themePreferenceStore = new JsonThemePreferenceStore();
        var startupTheme = ThemePreferenceMapper.ResolveStoredOrDefault(themeService, themePreferenceStore.Load()?.ThemeKey);
        themeService.ApplyTheme(startupTheme);

        var optionsValidator = new OpenPlatformOptionsValidator();
        var optionsProvider = new JsonOpenPlatformOptionsProvider();
        var mapOptionsProvider = new JsonMapProviderOptionsProvider();
        var tokenCacheRepository = new JsonTokenCacheRepository();
        var aiTaskStore = new JsonAiInspectionTaskStore();
        var aiAlertStore = new JsonAiAlertStore();
        var deviceAlarmStore = new JsonDeviceAlarmStore();
        var deviceCatalogCacheStore = new JsonDeviceCatalogCacheStore();
        var deviceInspectionStore = new JsonDeviceInspectionStore();
        var inspectionScopeStore = new JsonInspectionScopeStore();
        var manualCoordinateStore = new JsonManualCoordinateStore();
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
        var deviceCatalogService = new DeviceCatalogService(optionsProvider, tokenService, openPlatformClient, deviceCatalogCacheStore);
        var deviceInspectionService = new DeviceInspectionService(optionsProvider, tokenService, openPlatformClient, deviceInspectionStore);
        var manualCoordinateService = new ManualCoordinateService(manualCoordinateStore);
        var inspectionSelectionService = new InspectionSelectionService();
        var inspectionScopeService = new InspectionScopeService(deviceCatalogService, deviceInspectionService, manualCoordinateService, inspectionScopeStore);
        var mapInspectionPageViewModel = new MapInspectionPageViewModel(
            workspaceService.GetWorkspaceData(),
            inspectionScopeService,
            deviceInspectionService,
            manualCoordinateService,
            inspectionSelectionService,
            mapOptionsProvider.GetOptions());
        var pointGovernancePageViewModel = new PointGovernancePageViewModel(
            deviceCatalogService,
            deviceInspectionService,
            inspectionScopeService,
            inspectionSelectionService);
        var systemSettingsPageViewModel = new SystemSettingsPageViewModel(
            optionsProvider,
            platformConnectionService,
            tokenService,
            themeService,
            themePreferenceStore);
        var shellWindow = new ShellWindow
        {
            DataContext = new MainShellViewModel(
                workspaceService,
                aiInspectionCenterService,
                aiAlertService,
                deviceAlarmService,
                mapInspectionPageViewModel,
                pointGovernancePageViewModel,
                systemSettingsPageViewModel)
        };

        MainWindow = shellWindow;
        shellWindow.Show();
    }
}
