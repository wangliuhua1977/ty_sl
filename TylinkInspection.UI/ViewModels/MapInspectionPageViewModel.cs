using System.Collections.ObjectModel;
using System.Windows.Input;
using TylinkInspection.Core.Models;

namespace TylinkInspection.UI.ViewModels;

public sealed class MapInspectionPageViewModel : PageViewModelBase
{
    private SelectionItemViewModel? _selectedScheme;
    private SelectionItemViewModel? _selectedTaskType;

    public MapInspectionPageViewModel(InspectionWorkspaceData workspace)
        : base("地图巡检台", "默认进入地图巡检工作台，保留区域方案、地图主舞台、AI助手与待办占位结构。")
    {
        SchemeItems = new ObservableCollection<SelectionItemViewModel>
        {
            new() { Title = "方案一", Subtitle = "华东城市群", IsSelected = true },
            new() { Title = "方案二", Subtitle = "交通枢纽链路" },
            new() { Title = "方案三", Subtitle = "重点园区专项" }
        };

        TaskTypeItems = new ObservableCollection<SelectionItemViewModel>
        {
            new() { Title = "常规巡检", Subtitle = "按片区轮询", Badge = "128", IsSelected = true },
            new() { Title = "AI巡检", Subtitle = "异常态扫描", Badge = "42" },
            new() { Title = "复检任务", Subtitle = "人工复核", Badge = "9" }
        };

        OverviewMetrics = new ObservableCollection<OverviewMetric>(workspace.OverviewMetrics);
        AlertItems = new ObservableCollection<AlertItem>(workspace.AlertItems);
        MapMarkers = new ObservableCollection<MapMarker>(workspace.MapMarkers);
        ProgressItems = new ObservableCollection<ProgressItem>(workspace.ProgressItems);
        TodoItems = new ObservableCollection<TodoItem>(workspace.TodoItems);
        RadarSignals = new ObservableCollection<RadarSignal>(workspace.RadarSignals);

        CurrentTask = workspace.CurrentTask;
        AssistantStatus = workspace.AssistantStatus;
        LastUpdatedText = $"本地假数据 · {DateTime.Now:yyyy-MM-dd HH:mm}";

        _selectedScheme = SchemeItems.First(item => item.IsSelected);
        _selectedTaskType = TaskTypeItems.First(item => item.IsSelected);

        SelectSchemeCommand = new RelayCommand<SelectionItemViewModel>(item => SelectSingle(item, SchemeItems, value =>
        {
            _selectedScheme = value;
            RaisePropertyChanged(nameof(MapCaption));
        }));

        SelectTaskTypeCommand = new RelayCommand<SelectionItemViewModel>(item => SelectSingle(item, TaskTypeItems, value =>
        {
            _selectedTaskType = value;
            RaisePropertyChanged(nameof(MapCaption));
        }));
    }

    public ObservableCollection<SelectionItemViewModel> SchemeItems { get; }

    public ObservableCollection<SelectionItemViewModel> TaskTypeItems { get; }

    public ObservableCollection<OverviewMetric> OverviewMetrics { get; }

    public ObservableCollection<AlertItem> AlertItems { get; }

    public ObservableCollection<MapMarker> MapMarkers { get; }

    public ObservableCollection<ProgressItem> ProgressItems { get; }

    public ObservableCollection<TodoItem> TodoItems { get; }

    public ObservableCollection<RadarSignal> RadarSignals { get; }

    public CurrentTaskStatus CurrentTask { get; }

    public AssistantStatus AssistantStatus { get; }

    public string LastUpdatedText { get; }

    public string MapCaption => $"地图巡检台 / {_selectedScheme?.Title ?? "方案一"} / {_selectedTaskType?.Title ?? "常规巡检"}";

    public ICommand SelectSchemeCommand { get; }

    public ICommand SelectTaskTypeCommand { get; }

    private static void SelectSingle(SelectionItemViewModel? selectedItem, IEnumerable<SelectionItemViewModel> items, Action<SelectionItemViewModel> onSelected)
    {
        if (selectedItem is null)
        {
            return;
        }

        foreach (var item in items)
        {
            item.IsSelected = ReferenceEquals(item, selectedItem);
        }

        onSelected(selectedItem);
    }
}
