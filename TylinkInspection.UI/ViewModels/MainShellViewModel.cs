using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class MainShellViewModel : ObservableObject
{
    private readonly IInspectionSelectionService _inspectionSelectionService;
    private readonly IInspectionModuleNavigationService _navigationService;
    private ShellNavigationItemViewModel? _selectedNavigationItem;
    private PageViewModelBase? _currentPageViewModel;

    public MainShellViewModel(
        IInspectionWorkspaceService workspaceService,
        IAiInspectionTaskService aiInspectionTaskService,
        IInspectionScopeService inspectionScopeService,
        IInspectionSelectionService inspectionSelectionService,
        IAiAlertService aiAlertService,
        IDeviceAlarmService deviceAlarmService,
        IInspectionModuleNavigationService navigationService,
        MapInspectionPageViewModel mapInspectionPageViewModel,
        ReviewCenterPageViewModel reviewCenterPageViewModel,
        ReportCenterPageViewModel reportCenterPageViewModel,
        FaultClosureCenterPageViewModel faultClosureCenterPageViewModel,
        PointGovernancePageViewModel pointGovernancePageViewModel,
        SystemSettingsPageViewModel systemSettingsPageViewModel)
    {
        var workspace = workspaceService.GetWorkspaceData();
        _inspectionSelectionService = inspectionSelectionService;
        _navigationService = navigationService;

        NavigationItems = new ObservableCollection<ShellNavigationItemViewModel>
        {
            new()
            {
                PageKey = InspectionModulePageKeys.MapInspection,
                Title = "\u5730\u56fe\u5de1\u68c0\u53f0",
                PageViewModel = mapInspectionPageViewModel,
                IsSelected = true
            },
            new()
            {
                PageKey = InspectionModulePageKeys.AiInspectionCenter,
                Title = "AI\u667a\u80fd\u5de1\u68c0\u4e2d\u5fc3",
                PageViewModel = new AiInspectionCenterPageViewModel(
                    aiInspectionTaskService,
                    inspectionScopeService,
                    navigationService)
            },
            new()
            {
                PageKey = InspectionModulePageKeys.ReviewCenter,
                Title = "\u5de1\u68c0\u590d\u6838\u4e2d\u5fc3",
                PageViewModel = reviewCenterPageViewModel
            },
            new()
            {
                PageKey = "AiAlertCenter",
                Title = "AI\u544a\u8b66\u4e2d\u5fc3",
                PageViewModel = new AiAlertCenterPageViewModel(aiAlertService, deviceAlarmService)
            },
            new()
            {
                PageKey = InspectionModulePageKeys.PointGovernance,
                Title = "\u70b9\u4f4d\u6cbb\u7406\u4e2d\u5fc3",
                PageViewModel = pointGovernancePageViewModel
            },
            new()
            {
                PageKey = "StrategyConfig",
                Title = "\u7b56\u7565\u914d\u7f6e\u4e2d\u5fc3",
                PageViewModel = new StrategyConfigPageViewModel(workspace.StrategyConfigPage)
            },
            new()
            {
                PageKey = InspectionModulePageKeys.FaultClosure,
                Title = "\u6545\u969c\u95ed\u73af\u4e2d\u5fc3",
                PageViewModel = faultClosureCenterPageViewModel
            },
            new()
            {
                PageKey = "ReportCenter",
                Title = "\u62a5\u8868\u6c89\u6dc0\u4e2d\u5fc3",
                PageViewModel = reportCenterPageViewModel
            },
            new()
            {
                PageKey = "SystemSettings",
                Title = "\u7cfb\u7edf\u8bbe\u7f6e",
                PageViewModel = systemSettingsPageViewModel
            }
        };

        _selectedNavigationItem = NavigationItems.First(item => item.IsSelected);
        _currentPageViewModel = _selectedNavigationItem.PageViewModel;

        NavigateCommand = new RelayCommand<ShellNavigationItemViewModel>(NavigateTo);
        _navigationService.NavigationRequested += OnNavigationRequested;
    }

    public ObservableCollection<ShellNavigationItemViewModel> NavigationItems { get; }

    public PageViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set => SetProperty(ref _currentPageViewModel, value);
    }

    public ICommand NavigateCommand { get; }

    private void NavigateTo(ShellNavigationItemViewModel? navigationItem)
    {
        if (navigationItem is null || ReferenceEquals(_selectedNavigationItem, navigationItem))
        {
            return;
        }

        foreach (var item in NavigationItems)
        {
            item.IsSelected = ReferenceEquals(item, navigationItem);
        }

        _selectedNavigationItem = navigationItem;
        CurrentPageViewModel = navigationItem.PageViewModel;
    }

    private void OnNavigationRequested(object? sender, InspectionModuleNavigationRequestEventArgs e)
    {
        var context = e.Context;
        if (!string.IsNullOrWhiteSpace(context.DeviceCode))
        {
            _inspectionSelectionService.SetSelectedDevice(context.DeviceCode);
        }

        var targetItem = NavigationItems.FirstOrDefault(item =>
            string.Equals(item.PageKey, context.TargetPageKey, StringComparison.OrdinalIgnoreCase));
        if (targetItem is null)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            NavigateTo(targetItem);
            return;
        }

        dispatcher.Invoke(() => NavigateTo(targetItem));
    }
}
