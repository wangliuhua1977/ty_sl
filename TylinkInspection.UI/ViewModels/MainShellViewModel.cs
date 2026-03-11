using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Contracts;

namespace TylinkInspection.UI.ViewModels;

public sealed class MainShellViewModel : ObservableObject
{
    private ShellNavigationItemViewModel? _selectedNavigationItem;
    private PageViewModelBase? _currentPageViewModel;

    public MainShellViewModel(
        IInspectionWorkspaceService workspaceService,
        IAiInspectionTaskService aiInspectionTaskService,
        IInspectionScopeService inspectionScopeService,
        IAiAlertService aiAlertService,
        IDeviceAlarmService deviceAlarmService,
        MapInspectionPageViewModel mapInspectionPageViewModel,
        ReviewCenterPageViewModel reviewCenterPageViewModel,
        ReportCenterPageViewModel reportCenterPageViewModel,
        FaultClosureCenterPageViewModel faultClosureCenterPageViewModel,
        PointGovernancePageViewModel pointGovernancePageViewModel,
        SystemSettingsPageViewModel systemSettingsPageViewModel)
    {
        var workspace = workspaceService.GetWorkspaceData();

        NavigationItems = new ObservableCollection<ShellNavigationItemViewModel>
        {
            new() { Title = "\u5730\u56fe\u5de1\u68c0\u53f0", PageViewModel = mapInspectionPageViewModel, IsSelected = true },
            new() { Title = "AI\u667a\u80fd\u5de1\u68c0\u4e2d\u5fc3", PageViewModel = new AiInspectionCenterPageViewModel(aiInspectionTaskService, inspectionScopeService) },
            new() { Title = "\u5de1\u68c0\u590d\u6838\u4e2d\u5fc3", PageViewModel = reviewCenterPageViewModel },
            new() { Title = "AI\u544a\u8b66\u4e2d\u5fc3", PageViewModel = new AiAlertCenterPageViewModel(aiAlertService, deviceAlarmService) },
            new() { Title = "\u70b9\u4f4d\u6cbb\u7406\u4e2d\u5fc3", PageViewModel = pointGovernancePageViewModel },
            new() { Title = "\u7b56\u7565\u914d\u7f6e\u4e2d\u5fc3", PageViewModel = new StrategyConfigPageViewModel(workspace.StrategyConfigPage) },
            new() { Title = "\u6545\u969c\u95ed\u73af\u4e2d\u5fc3", PageViewModel = faultClosureCenterPageViewModel },
            new() { Title = "\u62a5\u8868\u6c89\u6dc0\u4e2d\u5fc3", PageViewModel = reportCenterPageViewModel },
            new() { Title = "\u7cfb\u7edf\u8bbe\u7f6e", PageViewModel = systemSettingsPageViewModel }
        };

        _selectedNavigationItem = NavigationItems.First(item => item.IsSelected);
        _currentPageViewModel = _selectedNavigationItem.PageViewModel;

        NavigateCommand = new RelayCommand<ShellNavigationItemViewModel>(NavigateTo);
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
}
