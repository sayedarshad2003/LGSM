using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LGMS.Desktop.ViewModels;

namespace LGMS.Desktop;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        UpdateTabVisibility(0);
    }

    private void OnTabSelected(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag != null && int.TryParse(rb.Tag.ToString(), out int tabIndex))
        {
            ViewModel.SelectedTabIndex = tabIndex;
            UpdateTabVisibility(tabIndex);
        }
    }

    private void UpdateTabVisibility(int tabIndex)
    {
        // Explicit tab panel visibility
        if (TabHistoryGrid != null) TabHistoryGrid.Visibility = (tabIndex == 1) ? Visibility.Visible : Visibility.Collapsed;
        if (TabBranchesGrid != null) TabBranchesGrid.Visibility = (tabIndex == 2) ? Visibility.Visible : Visibility.Collapsed;
        if (TabStashesGrid != null) TabStashesGrid.Visibility = (tabIndex == 3) ? Visibility.Visible : Visibility.Collapsed;
        if (TabRemotesGrid != null) TabRemotesGrid.Visibility = (tabIndex == 4) ? Visibility.Visible : Visibility.Collapsed;
        if (TabTerminalGrid != null) TabTerminalGrid.Visibility = (tabIndex == 5) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCreateBranchClicked(object sender, RoutedEventArgs e)
    {
        var branchName = NewBranchInput.Text.Trim();
        if (!string.IsNullOrEmpty(branchName))
        {
            ViewModel.CreateBranchCommand.Execute(branchName);
            NewBranchInput.Text = string.Empty;
        }
    }

    private void OnSaveStashClicked(object sender, RoutedEventArgs e)
    {
        var stashMsg = NewStashInput.Text.Trim();
        ViewModel.SaveStashCommand.Execute(stashMsg);
        NewStashInput.Text = string.Empty;
    }

    private void OnAddRemoteClicked(object sender, RoutedEventArgs e)
    {
        var name = RemoteNameInput.Text.Trim();
        var url = RemoteUrlInput.Text.Trim();
        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
        {
            ViewModel.AddRemoteCommand.Execute((name, url));
            RemoteNameInput.Text = string.Empty;
            RemoteUrlInput.Text = string.Empty;
        }
    }

    private void OnTerminalInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ExecuteTerminalCommand.Execute(null);
        }
    }
}