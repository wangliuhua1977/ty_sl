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
    private RecheckSchedulerService? _recheckSchedulerService;
    private AiInspectionTaskService? _aiInspectionTaskService;

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
        var aiTaskPlanStore = new JsonAiInspectionTaskPlanStore();
        var aiAlertStore = new JsonAiAlertStore();
        var deviceAlarmStore = new JsonDeviceAlarmStore();
        var deviceCatalogCacheStore = new JsonDeviceCatalogCacheStore();
        var deviceInspectionStore = new JsonDeviceInspectionStore();
        var playbackReviewStore = new JsonPlaybackReviewStore();
        var screenshotSampleStore = new JsonScreenshotSampleStore();
        var manualReviewStore = new JsonManualReviewStore();
        var faultClosureStore = new JsonFaultClosureStore();
        var recheckTaskStore = new JsonRecheckTaskStore();
        var cloudPlaybackCacheStore = new JsonCloudPlaybackCacheStore();
        var screenshotArtifactStore = new LocalScreenshotArtifactStore();
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
        var aiAlertService = new OpenPlatformAiAlertService(optionsProvider, tokenService, openPlatformClient, aiAlertStore);
        var deviceAlarmService = new OpenPlatformDeviceAlarmService(optionsProvider, tokenService, openPlatformClient, deviceAlarmStore);
        var deviceCatalogService = new DeviceCatalogService(optionsProvider, tokenService, openPlatformClient, deviceCatalogCacheStore);
        var deviceInspectionService = new DeviceInspectionService(optionsProvider, tokenService, openPlatformClient, deviceInspectionStore);
        var playbackReviewService = new PlaybackReviewService(optionsProvider, tokenService, openPlatformClient, playbackReviewStore, deviceInspectionService);
        var screenshotSamplingService = new ScreenshotSamplingService(screenshotSampleStore, screenshotArtifactStore);
        var cloudPlaybackService = new CloudPlaybackService(optionsProvider, tokenService, openPlatformClient, cloudPlaybackCacheStore);
        var manualCoordinateService = new ManualCoordinateService(manualCoordinateStore);
        var inspectionSelectionService = new InspectionSelectionService();
        var moduleNavigationService = new InspectionModuleNavigationService();
        var inspectionScopeService = new InspectionScopeService(deviceCatalogService, deviceInspectionService, manualCoordinateService, inspectionScopeStore);
        var faultClosureService = new FaultClosureService(
            faultClosureStore,
            manualReviewStore,
            screenshotSampleStore,
            playbackReviewStore,
            aiAlertService,
            inspectionScopeService,
            deviceCatalogService,
            deviceInspectionService);
        _recheckSchedulerService = new RecheckSchedulerService(
            recheckTaskStore,
            faultClosureService,
            deviceInspectionService,
            playbackReviewService);
        _recheckSchedulerService.Start();
        _aiInspectionTaskService = new AiInspectionTaskService(
            aiTaskStore,
            aiTaskPlanStore,
            inspectionScopeService,
            deviceCatalogService,
            deviceInspectionService,
            playbackReviewService,
            screenshotSamplingService,
            faultClosureService,
            _recheckSchedulerService);
        _aiInspectionTaskService.Start();
        var reviewCenterService = new ReviewCenterService(
            inspectionScopeService,
            aiAlertService,
            aiAlertStore,
            screenshotSampleStore,
            playbackReviewStore,
            manualReviewStore,
            faultClosureService);
        var reportCenterService = new ReportCenterService(
            inspectionScopeService,
            deviceInspectionStore,
            playbackReviewStore,
            screenshotSampleStore,
            manualReviewStore,
            faultClosureStore,
            recheckTaskStore);
        var mapInspectionPageViewModel = new MapInspectionPageViewModel(
            workspaceService.GetWorkspaceData(),
            inspectionScopeService,
            deviceInspectionService,
            manualCoordinateService,
            inspectionSelectionService,
            moduleNavigationService,
            _aiInspectionTaskService,
            faultClosureService,
            playbackReviewService,
            screenshotSamplingService,
            cloudPlaybackService,
            mapOptionsProvider.GetOptions());
        var pointGovernancePageViewModel = new PointGovernancePageViewModel(
            deviceCatalogService,
            deviceInspectionService,
            inspectionScopeService,
            inspectionSelectionService,
            moduleNavigationService,
            _aiInspectionTaskService,
            faultClosureService,
            playbackReviewService,
            screenshotSamplingService,
            cloudPlaybackService);
        var reviewCenterPageViewModel = new ReviewCenterPageViewModel(
            workspaceService.GetWorkspaceData().ReviewCenterPage,
            reviewCenterService,
            inspectionScopeService,
            inspectionSelectionService,
            moduleNavigationService,
            _aiInspectionTaskService,
            playbackReviewService,
            screenshotSamplingService,
            cloudPlaybackService);
        var reportCenterPageViewModel = new ReportCenterPageViewModel(
            workspaceService.GetWorkspaceData().ReportCenterPage,
            reportCenterService,
            inspectionScopeService);
        var faultClosureCenterPageViewModel = new FaultClosureCenterPageViewModel(
            workspaceService.GetWorkspaceData().FaultClosureCenterPage,
            faultClosureService,
            _recheckSchedulerService,
            inspectionScopeService,
            inspectionSelectionService,
            moduleNavigationService,
            _aiInspectionTaskService,
            deviceCatalogService,
            deviceInspectionService,
            playbackReviewService,
            screenshotSamplingService,
            cloudPlaybackService);
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
                _aiInspectionTaskService,
                inspectionScopeService,
                inspectionSelectionService,
                aiAlertService,
                deviceAlarmService,
                moduleNavigationService,
                mapInspectionPageViewModel,
                reviewCenterPageViewModel,
                reportCenterPageViewModel,
                faultClosureCenterPageViewModel,
                pointGovernancePageViewModel,
                systemSettingsPageViewModel)
        };

        MainWindow = shellWindow;
        shellWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _aiInspectionTaskService?.Stop();
        _recheckSchedulerService?.Stop();
        base.OnExit(e);
    }
}
