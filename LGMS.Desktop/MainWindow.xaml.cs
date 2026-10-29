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

        PreviewKeyDown += MainWindow_PreviewKeyDown;

        ViewModel.RequestIdeTextSelection += (start, length) =>
        {
            Dispatcher.Invoke(() =>
            {
                var textBox = FindName("IdeCodeTextBox") as TextBox;
                if (textBox != null && start >= 0 && start + length <= textBox.Text.Length)
                {
                    textBox.Focus();
                    textBox.Select(start, length);
                    var lineIdx = textBox.GetLineIndexFromCharacterIndex(start);
                    if (lineIdx >= 0)
                    {
                        textBox.ScrollToLine(lineIdx);
                    }
                }
            });
        };
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            ViewModel.CommitInspectorTabIndex = 1; // Switch to IDE view
            ViewModel.IsIdeSearchVisible = true;
            Dispatcher.InvokeAsync(() =>
            {
                var searchBox = FindName("IdeSearchTextBox") as TextBox;
                searchBox?.Focus();
                searchBox?.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
        else if (e.Key == Key.Escape && ViewModel.IsIdeSearchVisible)
        {
            e.Handled = true;
            ViewModel.IsIdeSearchVisible = false;
            var codeBox = FindName("IdeCodeTextBox") as TextBox;
            codeBox?.Focus();
        }
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