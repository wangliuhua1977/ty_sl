using System.Windows.Controls;
using System.Windows;
using TylinkInspection.UI.ViewModels;

namespace TylinkInspection.UI.Views.Pages;

public partial class PointGovernancePageView : UserControl
{
    public PointGovernancePageView()
    {
        InitializeComponent();
    }

    private void DirectoryTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is PointGovernancePageViewModel viewModel &&
            e.NewValue is InspectionDirectoryNodeViewModel node)
        {
            viewModel.SelectedDirectory = node;
        }
    }
}
