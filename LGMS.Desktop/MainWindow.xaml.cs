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
    }

    private void OnTabSelected(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag != null && int.TryParse(rb.Tag.ToString(), out int tabIndex))
        {
            ViewModel.SelectedTabIndex = tabIndex;
        }
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