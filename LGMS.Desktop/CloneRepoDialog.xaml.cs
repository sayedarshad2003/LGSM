using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace LGMS.Desktop;

public partial class CloneRepoDialog : Window
{
    public string DestinationFolder { get; private set; } = string.Empty;
    public string RepositoryUrl { get; private set; } = string.Empty;

    public CloneRepoDialog(string initialFolder, Window? owner = null)
    {
        InitializeComponent();
        if (owner != null) Owner = owner;

        DestinationFolder = initialFolder;
        DestFolderInput.Text = initialFolder;
        Loaded += (s, e) => RepoUrlInput.Focus();
    }

    private void OnChangeFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Destination Folder for Cloned Repository",
            InitialDirectory = Directory.Exists(DestinationFolder) ? DestinationFolder : @"C:\"
        };

        if (dialog.ShowDialog(this) == true)
        {
            DestinationFolder = dialog.FolderName;
            DestFolderInput.Text = dialog.FolderName;
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }
    }

    private void OnCloneClicked(object sender, RoutedEventArgs e)
    {
        var url = RepoUrlInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            ErrorMessageText.Text = "Please enter the remote repository URL.";
            ErrorMessageText.Visibility = Visibility.Visible;
            RepoUrlInput.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationFolder))
        {
            ErrorMessageText.Text = "Destination folder cannot be empty.";
            ErrorMessageText.Visibility = Visibility.Visible;
            return;
        }

        RepositoryUrl = url;
        DestinationFolder = DestFolderInput.Text.Trim();
        DialogResult = true;
        Close();
    }
}
