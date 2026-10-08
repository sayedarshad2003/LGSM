using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LGMS.Desktop.Common;
using LGMS.Models;
using LGMS.Services;
using Microsoft.Win32;

namespace LGMS.Desktop.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;
    private readonly IProcessService _processService;

    private string _workspacePath = string.Empty;
    private string _searchFilter = string.Empty;
    private Repository? _activeRepository;
    private GitStatus? _currentStatus;
    private bool _isBusy;
    private string _busyMessage = "Loading...";
    private string _statusMessage = "Ready";
    private string _statusSeverity = "info"; // info, success, warning, error
    private int _selectedTabIndex = 0;

    private string _commitSubject = string.Empty;
    private string _commitDescription = string.Empty;

    private GitChangeItemViewModel? _selectedChange;
    private string _rawDiffText = string.Empty;
    private ObservableCollection<DiffLineItem> _diffLines = new();

    private ObservableCollection<GitCommit> _commits = new();
    private GitCommit? _selectedCommit;
    private string _commitDiffText = string.Empty;

    private ObservableCollection<GitBranch> _branches = new();
    private ObservableCollection<GitStash> _stashes = new();
    private ObservableCollection<GitRemote> _remotes = new();

    private string _terminalInput = string.Empty;
    private string _terminalOutput = "Safe Git Terminal initialized. Type Git commands (e.g. status, log -n 5, branch -a).\n";

    private string _gitVersion = "Checking...";
    private bool _isGitInstalled;

    public MainViewModel(IGitService gitService, IRepositoryScanner scanner, IProcessService processService)
    {
        _gitService = gitService;
        _scanner = scanner;
        _processService = processService;

        AllRepositories = new ObservableCollection<Repository>();
        FilteredRepositories = new ObservableCollection<Repository>();
        StagedChanges = new ObservableCollection<GitChangeItemViewModel>();
        UnstagedChanges = new ObservableCollection<GitChangeItemViewModel>();

        // Init Commands
        BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync);
        ScanWorkspaceCommand = new AsyncRelayCommand(ScanWorkspaceAsync);
        SelectRepositoryCommand = new AsyncRelayCommand(p => SelectRepositoryAsync(p as Repository));
        RefreshCommand = new AsyncRelayCommand(RefreshActiveRepoAsync);
        FetchCommand = new AsyncRelayCommand(FetchAsync);
        PullCommand = new AsyncRelayCommand(PullAsync);
        PushCommand = new AsyncRelayCommand(PushAsync);

        StageAllCommand = new AsyncRelayCommand(StageAllAsync);
        UnstageAllCommand = new AsyncRelayCommand(UnstageAllAsync);
        StageItemCommand = new AsyncRelayCommand(p => StageItemAsync(p as GitChangeItemViewModel));
        UnstageItemCommand = new AsyncRelayCommand(p => UnstageItemAsync(p as GitChangeItemViewModel));
        DiscardItemCommand = new AsyncRelayCommand(p => DiscardItemAsync(p as GitChangeItemViewModel));
        DiscardSelectedCommand = new AsyncRelayCommand(DiscardSelectedAsync);

        CommitCommand = new AsyncRelayCommand(CommitAsync);
        CommitAndPushCommand = new AsyncRelayCommand(CommitAndPushAsync);

        CheckoutBranchCommand = new AsyncRelayCommand(p => CheckoutBranchAsync(p as string));
        CreateBranchCommand = new AsyncRelayCommand(p => CreateBranchAsync(p as string));
        DeleteBranchCommand = new AsyncRelayCommand(p => DeleteBranchAsync(p as string));

        SaveStashCommand = new AsyncRelayCommand(p => SaveStashAsync(p as string));
        ApplyStashCommand = new AsyncRelayCommand(p => ApplyStashAsync(p as string));
        PopStashCommand = new AsyncRelayCommand(p => PopStashAsync(p as string));
        DropStashCommand = new AsyncRelayCommand(p => DropStashAsync(p as string));

        AddRemoteCommand = new AsyncRelayCommand(p => AddRemoteAsync(p as (string Name, string Url)?));
        RemoveRemoteCommand = new AsyncRelayCommand(p => RemoveRemoteAsync(p as string));

        ExecuteTerminalCommand = new AsyncRelayCommand(ExecuteTerminalAsync);
        ClearTerminalCommand = new RelayCommand(ClearTerminal);

        OpenInExplorerCommand = new RelayCommand(OpenInExplorer);
        OpenInVsCodeCommand = new RelayCommand(OpenInVsCode);
        OpenTerminalCommand = new RelayCommand(OpenExternalTerminal);

        _ = InitializeAsync();
    }

    public ObservableCollection<Repository> AllRepositories { get; }
    public ObservableCollection<Repository> FilteredRepositories { get; }
    public ObservableCollection<GitChangeItemViewModel> StagedChanges { get; }
    public ObservableCollection<GitChangeItemViewModel> UnstagedChanges { get; }

    public ObservableCollection<GitCommit> Commits
    {
        get => _commits;
        set => SetProperty(ref _commits, value);
    }

    public ObservableCollection<GitBranch> Branches
    {
        get => _branches;
        set => SetProperty(ref _branches, value);
    }

    public ObservableCollection<GitStash> Stashes
    {
        get => _stashes;
        set => SetProperty(ref _stashes, value);
    }

    public ObservableCollection<GitRemote> Remotes
    {
        get => _remotes;
        set => SetProperty(ref _remotes, value);
    }

    public ObservableCollection<DiffLineItem> DiffLines
    {
        get => _diffLines;
        set => SetProperty(ref _diffLines, value);
    }

    public string WorkspacePath
    {
        get => _workspacePath;
        set
        {
            if (SetProperty(ref _workspacePath, value))
            {
                _ = ScanWorkspaceAsync();
            }
        }
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public Repository? ActiveRepository
    {
        get => _activeRepository;
        set
        {
            if (SetProperty(ref _activeRepository, value))
            {
                OnPropertyChanged(nameof(HasActiveRepo));
                _ = LoadActiveRepoDetailsAsync();
            }
        }
    }

    public bool HasActiveRepo => _activeRepository != null;

    public GitStatus? CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string StatusSeverity
    {
        get => _statusSeverity;
        set => SetProperty(ref _statusSeverity, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string CommitSubject
    {
        get => _commitSubject;
        set => SetProperty(ref _commitSubject, value);
    }

    public string CommitDescription
    {
        get => _commitDescription;
        set => SetProperty(ref _commitDescription, value);
    }

    public GitChangeItemViewModel? SelectedChange
    {
        get => _selectedChange;
        set
        {
            if (SetProperty(ref _selectedChange, value))
            {
                _ = LoadDiffForSelectedChangeAsync();
            }
        }
    }

    public string RawDiffText
    {
        get => _rawDiffText;
        set => SetProperty(ref _rawDiffText, value);
    }

    public GitCommit? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            if (SetProperty(ref _selectedCommit, value))
            {
                _ = LoadDiffForSelectedCommitAsync();
            }
        }
    }

    public string CommitDiffText
    {
        get => _commitDiffText;
        set => SetProperty(ref _commitDiffText, value);
    }

    public string TerminalInput
    {
        get => _terminalInput;
        set => SetProperty(ref _terminalInput, value);
    }

    public string TerminalOutput
    {
        get => _terminalOutput;
        set => SetProperty(ref _terminalOutput, value);
    }

    public string GitVersion
    {
        get => _gitVersion;
        set => SetProperty(ref _gitVersion, value);
    }

    public bool IsGitInstalled
    {
        get => _isGitInstalled;
        set => SetProperty(ref _isGitInstalled, value);
    }

    // Commands
    public ICommand BrowseFolderCommand { get; }
    public ICommand ScanWorkspaceCommand { get; }
    public ICommand SelectRepositoryCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand FetchCommand { get; }
    public ICommand PullCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand StageAllCommand { get; }
    public ICommand UnstageAllCommand { get; }
    public ICommand StageItemCommand { get; }
    public ICommand UnstageItemCommand { get; }
    public ICommand DiscardItemCommand { get; }
    public ICommand DiscardSelectedCommand { get; }
    public ICommand CommitCommand { get; }
    public ICommand CommitAndPushCommand { get; }
    public ICommand CheckoutBranchCommand { get; }
    public ICommand CreateBranchCommand { get; }
    public ICommand DeleteBranchCommand { get; }
    public ICommand SaveStashCommand { get; }
    public ICommand ApplyStashCommand { get; }
    public ICommand PopStashCommand { get; }
    public ICommand DropStashCommand { get; }
    public ICommand AddRemoteCommand { get; }
    public ICommand RemoveRemoteCommand { get; }
    public ICommand ExecuteTerminalCommand { get; }
    public ICommand ClearTerminalCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand OpenInVsCodeCommand { get; }
    public ICommand OpenTerminalCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            var res = await _processService.ExecuteAsync("git", new[] { "--version" }, Environment.CurrentDirectory);
            if (res.Success)
            {
                GitVersion = res.StandardOutput.Trim();
                IsGitInstalled = true;
            }
            else
            {
                GitVersion = "Git not detected in PATH";
                IsGitInstalled = false;
                SetNotification("Git executable was not found. Please install Git.", "error");
            }

            // Default to parent or current workspace
            var defaultPath = @"e:\Asp.NET Work\Test Project";
            if (Directory.Exists(defaultPath))
            {
                WorkspacePath = defaultPath;
            }
            else
            {
                WorkspacePath = Environment.CurrentDirectory;
            }
        }
        catch (Exception ex)
        {
            SetNotification($"Initialization error: {ex.Message}", "error");
        }
    }

    private Task BrowseFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Workspace Folder",
            InitialDirectory = Directory.Exists(WorkspacePath) ? WorkspacePath : @"C:\"
        };

        if (dialog.ShowDialog() == true)
        {
            WorkspacePath = dialog.FolderName;
        }

        return Task.CompletedTask;
    }

    public async Task ScanWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Scanning workspace for Git repositories...";

            var repos = await _scanner.ScanAsync(WorkspacePath);
            AllRepositories.Clear();
            foreach (var r in repos)
            {
                AllRepositories.Add(r);
            }
            ApplyFilter();

            SetNotification($"Found {AllRepositories.Count} repositories in workspace.", "info");

            if (ActiveRepository == null && AllRepositories.Count > 0)
            {
                ActiveRepository = AllRepositories[0];
            }
            else if (ActiveRepository != null)
            {
                var match = AllRepositories.FirstOrDefault(r => r.Path.Equals(ActiveRepository.Path, StringComparison.OrdinalIgnoreCase));
                if (match != null) ActiveRepository = match;
            }
        }
        catch (Exception ex)
        {
            SetNotification($"Scanning failed: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredRepositories.Clear();
        var query = SearchFilter?.Trim() ?? string.Empty;
        var items = string.IsNullOrWhiteSpace(query)
            ? AllRepositories
            : AllRepositories.Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || r.Path.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in items)
        {
            FilteredRepositories.Add(item);
        }
    }

    private async Task SelectRepositoryAsync(Repository? repo)
    {
        if (repo == null) return;
        ActiveRepository = repo;
        await LoadActiveRepoDetailsAsync();
    }

    public async Task RefreshActiveRepoAsync()
    {
        if (ActiveRepository == null) return;
        await LoadActiveRepoDetailsAsync();
        SetNotification("Repository status refreshed.", "info");
    }

    private async Task LoadActiveRepoDetailsAsync()
    {
        if (ActiveRepository == null) return;

        try
        {
            IsBusy = true;
            BusyMessage = $"Refreshing {ActiveRepository.Name}...";

            // Load Status & Changes
            CurrentStatus = await _gitService.ParseStatusAsync(ActiveRepository.Path);

            StagedChanges.Clear();
            UnstagedChanges.Clear();

            foreach (var change in CurrentStatus.Changes)
            {
                if (change.IsStaged)
                {
                    StagedChanges.Add(new GitChangeItemViewModel(change));
                }
                else
                {
                    UnstagedChanges.Add(new GitChangeItemViewModel(change));
                }
            }

            // Select first changed file if nothing selected
            if (SelectedChange == null || !CurrentStatus.Changes.Any(c => c.Path == SelectedChange.Path))
            {
                SelectedChange = StagedChanges.FirstOrDefault() ?? UnstagedChanges.FirstOrDefault();
            }
            else
            {
                await LoadDiffForSelectedChangeAsync();
            }

            // Load Commits
            await LoadCommitsAsync();

            // Load Branches
            await LoadBranchesAsync();

            // Load Stashes
            await LoadStashesAsync();

            // Load Remotes
            await LoadRemotesAsync();
        }
        catch (Exception ex)
        {
            SetNotification($"Error loading repository: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCommitsAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.GetCommitsAsync(ActiveRepository.Path, 40);
        Commits.Clear();

        if (res.Success && !string.IsNullOrWhiteSpace(res.Output))
        {
            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 7)
                {
                    Commits.Add(new GitCommit
                    {
                        Hash = parts[0],
                        AuthorName = parts[2],
                        AuthorEmail = parts[3],
                        Date = DateTime.TryParse(parts[4], out var dt) ? dt : DateTime.UtcNow,
                        RelativeTime = parts[5],
                        Subject = parts[6],
                        Body = parts.Length > 7 ? parts[7] : string.Empty
                    });
                }
            }
        }
    }

    private async Task LoadBranchesAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.GetBranchesAsync(ActiveRepository.Path);
        Branches.Clear();

        if (res.Success && !string.IsNullOrWhiteSpace(res.Output))
        {
            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 1)
                {
                    var name = parts[0].Trim();
                    var isHead = parts.Length > 1 && parts[1].Trim() == "*";
                    var isRemote = name.StartsWith("origin/") || name.StartsWith("remotes/");

                    Branches.Add(new GitBranch
                    {
                        Name = name,
                        IsCurrent = isHead,
                        IsRemote = isRemote,
                        Upstream = parts.Length > 2 ? parts[2] : null,
                        LatestCommitHash = parts.Length > 3 ? parts[3] : string.Empty,
                        LatestCommitSubject = parts.Length > 4 ? parts[4] : string.Empty
                    });
                }
            }
        }
    }

    private async Task LoadStashesAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.GetStashesAsync(ActiveRepository.Path);
        Stashes.Clear();

        if (res.Success && !string.IsNullOrWhiteSpace(res.Output))
        {
            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int idx = 0;
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 2)
                {
                    Stashes.Add(new GitStash
                    {
                        Index = idx++,
                        Message = parts[1].Trim(),
                        Date = parts.Length > 2 ? parts[2].Trim() : string.Empty
                    });
                }
            }
        }
    }

    private async Task LoadRemotesAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.GetRemotesAsync(ActiveRepository.Path);
        Remotes.Clear();

        if (res.Success && !string.IsNullOrWhiteSpace(res.Output))
        {
            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && seen.Add(parts[0]))
                {
                    Remotes.Add(new GitRemote
                    {
                        Name = parts[0],
                        FetchUrl = parts[1],
                        PushUrl = parts[1]
                    });
                }
            }
        }
    }

    private async Task LoadDiffForSelectedChangeAsync()
    {
        if (ActiveRepository == null || SelectedChange == null)
        {
            RawDiffText = string.Empty;
            DiffLines.Clear();
            return;
        }

        try
        {
            var res = await _gitService.DiffAsync(ActiveRepository.Path, SelectedChange.IsStaged, SelectedChange.Path);
            RawDiffText = res.Success ? res.Output : $"Failed to load diff: {res.Error}";
            ParseDiffLines(RawDiffText);
        }
        catch (Exception ex)
        {
            RawDiffText = $"Diff error: {ex.Message}";
            DiffLines.Clear();
        }
    }

    private async Task LoadDiffForSelectedCommitAsync()
    {
        if (ActiveRepository == null || SelectedCommit == null)
        {
            CommitDiffText = string.Empty;
            return;
        }

        try
        {
            var res = await _processService.ExecuteAsync("git", new[] { "show", "--stat", "--patch", SelectedCommit.Hash }, ActiveRepository.Path);
            CommitDiffText = res.Success ? res.StandardOutput : res.StandardError;
        }
        catch (Exception ex)
        {
            CommitDiffText = $"Error loading commit details: {ex.Message}";
        }
    }

    private void ParseDiffLines(string diff)
    {
        DiffLines.Clear();
        if (string.IsNullOrWhiteSpace(diff))
        {
            DiffLines.Add(new DiffLineItem { Text = "(No diff or binary file)", LineType = "context" });
            return;
        }

        var lines = diff.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int oldLine = 0;
        int newLine = 0;

        foreach (var line in lines)
        {
            var item = new DiffLineItem { Text = line };

            if (line.StartsWith("@@"))
            {
                item.LineType = "header";
                // parse @@ -old,count +new,count @@
                var match = System.Text.RegularExpressions.Regex.Match(line, @"@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@");
                if (match.Success)
                {
                    int.TryParse(match.Groups[1].Value, out oldLine);
                    int.TryParse(match.Groups[2].Value, out newLine);
                }
            }
            else if (line.StartsWith("+") && !line.StartsWith("+++"))
            {
                item.LineType = "add";
                item.NewLineNumber = newLine++;
            }
            else if (line.StartsWith("-") && !line.StartsWith("---"))
            {
                item.LineType = "delete";
                item.OldLineNumber = oldLine++;
            }
            else
            {
                item.LineType = "context";
                if (oldLine > 0) item.OldLineNumber = oldLine++;
                if (newLine > 0) item.NewLineNumber = newLine++;
            }

            DiffLines.Add(item);
        }
    }

    private async Task FetchAsync()
    {
        if (ActiveRepository == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = "Fetching remote updates...";
            var res = await _gitService.FetchAsync(ActiveRepository.Path);
            if (res.Success)
            {
                SetNotification("Fetched all remotes successfully.", "success");
                await LoadActiveRepoDetailsAsync();
            }
            else
            {
                SetNotification($"Fetch failed: {res.Error}", "error");
            }
        }
        finally { IsBusy = false; }
    }

    private async Task PullAsync()
    {
        if (ActiveRepository == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = "Pulling latest changes...";
            var res = await _gitService.PullAsync(ActiveRepository.Path);
            if (res.Success)
            {
                SetNotification("Pulled changes successfully.", "success");
                await LoadActiveRepoDetailsAsync();
            }
            else
            {
                SetNotification($"Pull error: {res.Error}", "error");
            }
        }
        finally { IsBusy = false; }
    }

    private async Task PushAsync()
    {
        if (ActiveRepository == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = "Pushing commits to remote...";
            var res = await _gitService.PushAsync(ActiveRepository.Path);
            if (res.Success)
            {
                SetNotification("Pushed successfully to remote.", "success");
                await LoadActiveRepoDetailsAsync();
            }
            else
            {
                SetNotification($"Push error: {res.Error}", "error");
            }
        }
        finally { IsBusy = false; }
    }

    private async Task StageAllAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.StageAsync(ActiveRepository.Path, new[] { "." });
        if (res.Success)
        {
            await LoadActiveRepoDetailsAsync();
            SetNotification("Staged all changes.", "success");
        }
        else
        {
            SetNotification($"Failed to stage: {res.Error}", "error");
        }
    }

    private async Task UnstageAllAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.UnstageAsync(ActiveRepository.Path, new[] { "." });
        if (res.Success)
        {
            await LoadActiveRepoDetailsAsync();
            SetNotification("Unstaged all changes.", "success");
        }
        else
        {
            SetNotification($"Failed to unstage: {res.Error}", "error");
        }
    }

    private async Task StageItemAsync(GitChangeItemViewModel? item)
    {
        if (ActiveRepository == null || item == null) return;
        var res = await _gitService.StageAsync(ActiveRepository.Path, new[] { item.Path });
        if (res.Success)
        {
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Stage failed: {res.Error}", "error");
        }
    }

    private async Task UnstageItemAsync(GitChangeItemViewModel? item)
    {
        if (ActiveRepository == null || item == null) return;
        var res = await _gitService.UnstageAsync(ActiveRepository.Path, new[] { item.Path });
        if (res.Success)
        {
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Unstage failed: {res.Error}", "error");
        }
    }

    private async Task DiscardItemAsync(GitChangeItemViewModel? item)
    {
        if (ActiveRepository == null || item == null) return;
        var result = MessageBox.Show($"Are you sure you want to discard changes in '{item.Path}'? This cannot be undone.", "Discard Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var res = await _gitService.DiscardAsync(ActiveRepository.Path, new[] { item.Path });
        if (res.Success)
        {
            SetNotification($"Discarded '{item.Path}'.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Discard failed: {res.Error}", "error");
        }
    }

    private async Task DiscardSelectedAsync()
    {
        if (ActiveRepository == null) return;
        var selected = UnstagedChanges.Where(x => x.IsSelected).Select(x => x.Path).ToList();
        if (selected.Count == 0)
        {
            SetNotification("No unstaged files selected to discard.", "warning");
            return;
        }

        var result = MessageBox.Show($"Are you sure you want to discard changes in {selected.Count} file(s)? This cannot be undone.", "Discard Multiple Files", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var res = await _gitService.DiscardAsync(ActiveRepository.Path, selected);
        if (res.Success)
        {
            SetNotification($"Discarded {selected.Count} file(s).", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Discard failed: {res.Error}", "error");
        }
    }

    private async Task CommitAsync()
    {
        if (ActiveRepository == null) return;

        if (string.IsNullOrWhiteSpace(CommitSubject))
        {
            SetNotification("Please enter a commit message.", "warning");
            return;
        }

        if (StagedChanges.Count == 0)
        {
            SetNotification("No staged changes to commit. Stage files first.", "warning");
            return;
        }

        var fullMessage = string.IsNullOrWhiteSpace(CommitDescription)
            ? CommitSubject.Trim()
            : $"{CommitSubject.Trim()}\n\n{CommitDescription.Trim()}";

        var res = await _gitService.CommitAsync(ActiveRepository.Path, fullMessage);
        if (res.Success)
        {
            CommitSubject = string.Empty;
            CommitDescription = string.Empty;
            SetNotification("Committed changes successfully.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Commit failed: {res.Error}", "error");
        }
    }

    private async Task CommitAndPushAsync()
    {
        await CommitAsync();
        if (string.IsNullOrEmpty(CommitSubject)) // Meaning commit succeeded
        {
            await PushAsync();
        }
    }

    private async Task CheckoutBranchAsync(string? branch)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(branch)) return;
        var res = await _gitService.CheckoutAsync(ActiveRepository.Path, branch);
        if (res.Success)
        {
            SetNotification($"Checked out branch '{branch}'.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Checkout failed: {res.Error}", "error");
        }
    }

    private async Task CreateBranchAsync(string? branchName)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(branchName)) return;
        var res = await _gitService.CreateBranchAsync(ActiveRepository.Path, branchName);
        if (res.Success)
        {
            SetNotification($"Created and switched to branch '{branchName}'.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Branch creation failed: {res.Error}", "error");
        }
    }

    private async Task DeleteBranchAsync(string? branchName)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(branchName)) return;
        var confirm = MessageBox.Show($"Delete branch '{branchName}'?", "Confirm Delete Branch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await _gitService.DeleteBranchAsync(ActiveRepository.Path, branchName);
        if (res.Success)
        {
            SetNotification($"Deleted branch '{branchName}'.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Delete branch failed: {res.Error}", "error");
        }
    }

    private async Task SaveStashAsync(string? message)
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.StashSaveAsync(ActiveRepository.Path, message);
        if (res.Success)
        {
            SetNotification("Changes saved to stash.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Stash failed: {res.Error}", "error");
        }
    }

    private async Task ApplyStashAsync(string? stashRef)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(stashRef)) return;
        var res = await _gitService.StashApplyAsync(ActiveRepository.Path, stashRef);
        if (res.Success)
        {
            SetNotification($"Applied stash '{stashRef}'.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Apply stash error: {res.Error}", "error");
        }
    }

    private async Task PopStashAsync(string? stashRef)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(stashRef)) return;
        var res = await _gitService.StashPopAsync(ActiveRepository.Path, stashRef);
        if (res.Success)
        {
            SetNotification($"Popped stash '{stashRef}'.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Pop stash error: {res.Error}", "error");
        }
    }

    private async Task DropStashAsync(string? stashRef)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(stashRef)) return;
        var res = await _gitService.StashDropAsync(ActiveRepository.Path, stashRef);
        if (res.Success)
        {
            SetNotification($"Dropped stash '{stashRef}'.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Drop stash error: {res.Error}", "error");
        }
    }

    private async Task AddRemoteAsync((string Name, string Url)? remote)
    {
        if (ActiveRepository == null || !remote.HasValue) return;
        var res = await _gitService.AddRemoteAsync(ActiveRepository.Path, remote.Value.Name, remote.Value.Url);
        if (res.Success)
        {
            SetNotification($"Added remote '{remote.Value.Name}'.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Add remote error: {res.Error}", "error");
        }
    }

    private async Task RemoveRemoteAsync(string? name)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(name)) return;
        var res = await _gitService.RemoveRemoteAsync(ActiveRepository.Path, name);
        if (res.Success)
        {
            SetNotification($"Removed remote '{name}'.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Remove remote error: {res.Error}", "error");
        }
    }

    private async Task ExecuteTerminalAsync()
    {
        if (ActiveRepository == null)
        {
            SetNotification("No active repository selected.", "warning");
            return;
        }

        if (string.IsNullOrWhiteSpace(TerminalInput)) return;

        var cmd = TerminalInput.Trim();
        TerminalInput = string.Empty;
        TerminalOutput += $"\n> git {cmd}\n";

        var res = await _gitService.RunCommandAsync(ActiveRepository.Path, cmd);
        if (!string.IsNullOrEmpty(res.Output))
        {
            TerminalOutput += res.Output + "\n";
        }
        if (!string.IsNullOrEmpty(res.Error))
        {
            TerminalOutput += $"[Error] {res.Error}\n";
        }

        if (res.ExitCode == 0)
        {
            await LoadActiveRepoDetailsAsync();
        }
    }

    private void ClearTerminal()
    {
        TerminalOutput = string.Empty;
    }

    private void OpenInExplorer()
    {
        if (ActiveRepository != null && Directory.Exists(ActiveRepository.Path))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = ActiveRepository.Path, UseShellExecute = true });
        }
    }

    private void OpenInVsCode()
    {
        if (ActiveRepository != null && Directory.Exists(ActiveRepository.Path))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "code", Arguments = $"\"{ActiveRepository.Path}\"", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SetNotification($"VS Code launch error: {ex.Message}", "error");
            }
        }
    }

    private void OpenExternalTerminal()
    {
        if (ActiveRepository != null && Directory.Exists(ActiveRepository.Path))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "powershell.exe", WorkingDirectory = ActiveRepository.Path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SetNotification($"Terminal launch error: {ex.Message}", "error");
            }
        }
    }

    private void SetNotification(string message, string severity = "info")
    {
        StatusMessage = message;
        StatusSeverity = severity;
    }
}
