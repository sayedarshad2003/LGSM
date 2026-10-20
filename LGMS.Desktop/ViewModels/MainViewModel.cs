using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LGMS.Desktop.Common;
using LGMS.Desktop.Services;
using LGMS.Models;
using LGMS.Services;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace LGMS.Desktop.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;
    private readonly IProcessService _processService;
    private readonly ISettingsService _settingsService;
    private readonly GitHubCliService _githubCliService;
    private readonly IAICommitManager? _aiManager;
    private readonly GeminiOptions? _geminiOptions;
    private readonly DeepSeekOptions? _deepSeekOptions;
    private readonly AIOptions? _aiOptions;

    // AI Commit Generation fields
    private ObservableCollection<string> _availableAiProviders = new();
    private string _selectedAiProvider = "Gemini";
    private ObservableCollection<string> _availableAiModels = new();
    private string _selectedAiModel = "gemini-3.5-flash-lite";
    private bool _isAiGenerating;
    private string _aiStatusMessage = string.Empty;
    private string _aiStatusSeverity = "info"; // info, success, warning, error

    // AI Settings fields
    private string _geminiApiKeyInput = string.Empty;
    private string _deepSeekApiKeyInput = string.Empty;
    private bool _isAiTesting;
    private string _aiTestStatus = string.Empty;
    private string _aiTestSeverity = "info";

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

    // GitHub Integration fields
    private GitHubAccountInfo? _selectedGitHubAccount;
    private ObservableCollection<GitHubAccountInfo> _gitHubAccounts = new();
    private ObservableCollection<GitHubRepositoryInfo> _gitHubRepositories = new();
    private ObservableCollection<GitHubRepositoryInfo> _filteredGitHubRepositories = new();
    private string _gitHubSearchFilter = string.Empty;
    private string _gitHubCliStatus = "Ready";

    // Settings fields
    private string _currentTheme = "dark";
    private string _defaultWorkspacePath = string.Empty;
    private string _gitUserName = string.Empty;
    private string _gitUserEmail = string.Empty;
    private bool _autoScanOnStartup = true;

    public MainViewModel(
        IGitService gitService,
        IRepositoryScanner scanner,
        IProcessService processService,
        ISettingsService settingsService,
        GitHubCliService githubCliService,
        IAICommitManager? aiManager = null,
        IOptions<GeminiOptions>? geminiOptions = null,
        IOptions<DeepSeekOptions>? deepSeekOptions = null,
        IOptions<AIOptions>? aiOptions = null)
    {
        _gitService = gitService;
        _scanner = scanner;
        _processService = processService;
        _settingsService = settingsService;
        _githubCliService = githubCliService;
        _aiManager = aiManager;
        _geminiOptions = geminiOptions?.Value;
        _deepSeekOptions = deepSeekOptions?.Value;
        _aiOptions = aiOptions?.Value;

        AllRepositories = new ObservableCollection<Repository>();
        FilteredRepositories = new ObservableCollection<Repository>();
        StagedChanges = new ObservableCollection<GitChangeItemViewModel>();
        UnstagedChanges = new ObservableCollection<GitChangeItemViewModel>();

        // Init AI Setup
        InitializeAiProvidersAndModels();

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

        AutoGenerateCommitCommand = new AsyncRelayCommand(AutoGenerateCommitAsync);
        TestAiConnectionCommand = new AsyncRelayCommand(p => TestAiConnectionAsync(p as string));
        SaveAiSettingsCommand = new RelayCommand(SaveAiSettings);

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

        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        SaveGitConfigCommand = new AsyncRelayCommand(SaveGitConfigAsync);

        // GitHub Commands
        RefreshGitHubAccountsCommand = new AsyncRelayCommand(LoadGitHubAccountsAsync);
        SwitchGitHubAccountCommand = new AsyncRelayCommand(p => SwitchGitHubAccountAsync(p as GitHubAccountInfo));
        LoadGitHubRepositoriesCommand = new AsyncRelayCommand(LoadGitHubRepositoriesAsync);
        CloneGitHubRepoCommand = new AsyncRelayCommand(p => CloneGitHubRepoAsync(p as GitHubRepositoryInfo));
        SetupGitCredentialsCommand = new AsyncRelayCommand(SetupGitCredentialsAsync);
        OpenUrlCommand = new RelayCommand(p => OpenUrl(p as string));
        CopyCommand = new RelayCommand(p => CopyToClipboard(p as string));

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
                ApplyRepositoryFilter();
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
        set
        {
            if (SetProperty(ref _currentStatus, value))
            {
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public bool HasChanges => _currentStatus != null && _currentStatus.Changes.Count > 0;

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
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                // Reload tab specific view if necessary
                if (value == 0 && SelectedChange != null)
                {
                    _ = LoadDiffForSelectedChangeAsync();
                }
                else if (value == 7 && GitHubAccounts.Count == 0)
                {
                    _ = LoadGitHubAccountsAsync();
                }
            }
        }
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

    // AI Commit Generation & Selection Properties
    public ObservableCollection<string> AvailableAiProviders
    {
        get => _availableAiProviders;
        set => SetProperty(ref _availableAiProviders, value);
    }

    public string SelectedAiProvider
    {
        get => _selectedAiProvider;
        set
        {
            if (SetProperty(ref _selectedAiProvider, value))
            {
                UpdateAiModelsForProvider(value);
            }
        }
    }

    public ObservableCollection<string> AvailableAiModels
    {
        get => _availableAiModels;
        set => SetProperty(ref _availableAiModels, value);
    }

    public string SelectedAiModel
    {
        get => _selectedAiModel;
        set => SetProperty(ref _selectedAiModel, value);
    }

    public bool IsAiGenerating
    {
        get => _isAiGenerating;
        set => SetProperty(ref _isAiGenerating, value);
    }

    public string AiStatusMessage
    {
        get => _aiStatusMessage;
        set => SetProperty(ref _aiStatusMessage, value);
    }

    public string AiStatusSeverity
    {
        get => _aiStatusSeverity;
        set => SetProperty(ref _aiStatusSeverity, value);
    }

    // AI Settings Properties
    public string GeminiApiKeyInput
    {
        get => _geminiApiKeyInput;
        set => SetProperty(ref _geminiApiKeyInput, value);
    }

    public string DeepSeekApiKeyInput
    {
        get => _deepSeekApiKeyInput;
        set => SetProperty(ref _deepSeekApiKeyInput, value);
    }

    public bool IsAiTesting
    {
        get => _isAiTesting;
        set => SetProperty(ref _isAiTesting, value);
    }

    public string AiTestStatus
    {
        get => _aiTestStatus;
        set => SetProperty(ref _aiTestStatus, value);
    }

    public string AiTestSeverity
    {
        get => _aiTestSeverity;
        set => SetProperty(ref _aiTestSeverity, value);
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

    // GitHub Integration Properties
    public ObservableCollection<GitHubAccountInfo> GitHubAccounts
    {
        get => _gitHubAccounts;
        set => SetProperty(ref _gitHubAccounts, value);
    }

    public GitHubAccountInfo? SelectedGitHubAccount
    {
        get => _selectedGitHubAccount;
        set
        {
            if (SetProperty(ref _selectedGitHubAccount, value))
            {
                _ = LoadGitHubRepositoriesAsync();
            }
        }
    }

    public ObservableCollection<GitHubRepositoryInfo> GitHubRepositories
    {
        get => _gitHubRepositories;
        set => SetProperty(ref _gitHubRepositories, value);
    }

    public ObservableCollection<GitHubRepositoryInfo> FilteredGitHubRepositories
    {
        get => _filteredGitHubRepositories;
        set => SetProperty(ref _filteredGitHubRepositories, value);
    }

    public string GitHubSearchFilter
    {
        get => _gitHubSearchFilter;
        set
        {
            if (SetProperty(ref _gitHubSearchFilter, value))
            {
                ApplyGitHubFilter();
            }
        }
    }

    public string GitHubCliStatus
    {
        get => _gitHubCliStatus;
        set => SetProperty(ref _gitHubCliStatus, value);
    }

    // Settings Properties
    public string CurrentTheme
    {
        get => _currentTheme;
        set => SetProperty(ref _currentTheme, value);
    }

    public string DefaultWorkspacePath
    {
        get => _defaultWorkspacePath;
        set => SetProperty(ref _defaultWorkspacePath, value);
    }

    public string GitUserName
    {
        get => _gitUserName;
        set => SetProperty(ref _gitUserName, value);
    }

    public string GitUserEmail
    {
        get => _gitUserEmail;
        set => SetProperty(ref _gitUserEmail, value);
    }

    public bool AutoScanOnStartup
    {
        get => _autoScanOnStartup;
        set => SetProperty(ref _autoScanOnStartup, value);
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
    public ICommand AutoGenerateCommitCommand { get; }
    public ICommand TestAiConnectionCommand { get; }
    public ICommand SaveAiSettingsCommand { get; }
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
    public ICommand ToggleThemeCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SaveGitConfigCommand { get; }

    // GitHub Commands
    public ICommand RefreshGitHubAccountsCommand { get; }
    public ICommand SwitchGitHubAccountCommand { get; }
    public ICommand LoadGitHubRepositoriesCommand { get; }
    public ICommand CloneGitHubRepoCommand { get; }
    public ICommand SetupGitCredentialsCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand CopyCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            // Load saved settings
            var savedSettings = _settingsService.LoadSettings();
            CurrentTheme = savedSettings.Theme;
            DefaultWorkspacePath = string.IsNullOrWhiteSpace(savedSettings.WorkingFolder)
                ? @"e:\Asp.NET Work\Test Project"
                : savedSettings.WorkingFolder;
            AutoScanOnStartup = savedSettings.AutoScanRepositories;

            ThemeManager.ApplyTheme(CurrentTheme);

            // Check Git
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

            // Load Global Git Config user.name / user.email
            var nameRes = await _processService.ExecuteAsync("git", new[] { "config", "--global", "user.name" }, Environment.CurrentDirectory);
            if (nameRes.Success) GitUserName = nameRes.StandardOutput.Trim();

            var emailRes = await _processService.ExecuteAsync("git", new[] { "config", "--global", "user.email" }, Environment.CurrentDirectory);
            if (emailRes.Success) GitUserEmail = emailRes.StandardOutput.Trim();

            // Load saved AI Preferences
            if (!string.IsNullOrWhiteSpace(savedSettings.ActiveAiProvider))
            {
                SelectedAiProvider = savedSettings.ActiveAiProvider;
            }
            if (!string.IsNullOrWhiteSpace(savedSettings.SelectedAiModel))
            {
                SelectedAiModel = savedSettings.SelectedAiModel;
            }
            GeminiApiKeyInput = !string.IsNullOrWhiteSpace(savedSettings.GeminiApiKey)
                ? savedSettings.GeminiApiKey
                : (_geminiOptions?.ApiKey ?? string.Empty);
            DeepSeekApiKeyInput = !string.IsNullOrWhiteSpace(savedSettings.DeepSeekApiKey)
                ? savedSettings.DeepSeekApiKey
                : (_deepSeekOptions?.ApiKey ?? string.Empty);

            // Set initial workspace path
            if (Directory.Exists(DefaultWorkspacePath))
            {
                WorkspacePath = DefaultWorkspacePath;
            }
            else
            {
                WorkspacePath = Environment.CurrentDirectory;
            }

            // Preload GitHub accounts in background
            _ = LoadGitHubAccountsAsync();
        }
        catch (Exception ex)
        {
            SetNotification($"Initialization error: {ex.Message}", "error");
        }
    }

    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == "dark" ? "light" : "dark";
        SaveSettings();
    }

    public void SaveSettings()
    {
        var settings = new WorkspaceSettings
        {
            WorkingFolder = DefaultWorkspacePath,
            Theme = CurrentTheme,
            AutoScanRepositories = AutoScanOnStartup,
            ActiveAiProvider = SelectedAiProvider,
            SelectedAiModel = SelectedAiModel,
            GeminiApiKey = GeminiApiKeyInput,
            DeepSeekApiKey = DeepSeekApiKeyInput
        };

        _settingsService.SaveSettings(settings);
        ThemeManager.ApplyTheme(CurrentTheme);
        SetNotification("Settings saved successfully.", "success");
    }

    public async Task SaveGitConfigAsync()
    {
        if (string.IsNullOrWhiteSpace(GitUserName) || string.IsNullOrWhiteSpace(GitUserEmail))
        {
            SetNotification("Name and email cannot be empty.", "warning");
            return;
        }

        await _processService.ExecuteAsync("git", new[] { "config", "--global", "user.name", GitUserName.Trim() }, Environment.CurrentDirectory);
        await _processService.ExecuteAsync("git", new[] { "config", "--global", "user.email", GitUserEmail.Trim() }, Environment.CurrentDirectory);
        SetNotification("Global Git author credentials saved.", "success");
    }

    public Task BrowseFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Workspace Root Folder",
            InitialDirectory = Directory.Exists(WorkspacePath) ? WorkspacePath : @"C:\"
        };

        if (dialog.ShowDialog() == true)
        {
            WorkspacePath = dialog.FolderName;
            DefaultWorkspacePath = dialog.FolderName;
            SaveSettings();
        }

        return Task.CompletedTask;
    }

    public async Task ScanWorkspaceAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath) || !Directory.Exists(WorkspacePath)) return;

        try
        {
            IsBusy = true;
            BusyMessage = "Scanning workspace for repositories...";

            var repos = await _scanner.ScanAsync(WorkspacePath);
            AllRepositories.Clear();
            foreach (var r in repos)
            {
                AllRepositories.Add(r);
            }

            ApplyRepositoryFilter();

            if (ActiveRepository != null)
            {
                var refreshedActive = AllRepositories.FirstOrDefault(r => r.Path.Equals(ActiveRepository.Path, StringComparison.OrdinalIgnoreCase));
                if (refreshedActive != null)
                {
                    ActiveRepository = refreshedActive;
                }
            }
            else if (AllRepositories.Count > 0)
            {
                ActiveRepository = AllRepositories[0];
            }

            SetNotification($"Found {AllRepositories.Count} repositories in workspace.", "info");
        }
        catch (Exception ex)
        {
            SetNotification($"Scan error: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyRepositoryFilter()
    {
        FilteredRepositories.Clear();
        var q = (SearchFilter ?? string.Empty).Trim();
        var list = string.IsNullOrEmpty(q)
            ? AllRepositories
            : new ObservableCollection<Repository>(AllRepositories.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.CurrentBranch.Contains(q, StringComparison.OrdinalIgnoreCase)));

        foreach (var r in list)
        {
            FilteredRepositories.Add(r);
        }
    }

    public Task SelectRepositoryAsync(Repository? repo)
    {
        if (repo != null)
        {
            ActiveRepository = repo;
        }
        return Task.CompletedTask;
    }

    public async Task RefreshActiveRepoAsync()
    {
        if (ActiveRepository == null) return;
        await LoadActiveRepoDetailsAsync();
        SetNotification($"Refreshed status for {ActiveRepository.Name}.", "info");
    }

    private async Task LoadActiveRepoDetailsAsync()
    {
        if (ActiveRepository == null || !Directory.Exists(ActiveRepository.Path)) return;

        try
        {
            IsBusy = true;
            BusyMessage = $"Checking {ActiveRepository.Name}...";

            CurrentStatus = await _gitService.ParseStatusAsync(ActiveRepository.Path);

            // Update repo summary properties
            ActiveRepository.CurrentBranch = CurrentStatus.CurrentBranch;
            ActiveRepository.ChangesCount = CurrentStatus.Changes.Count;
            ActiveRepository.IsClean = CurrentStatus.Changes.Count == 0;
            ActiveRepository.AheadCount = CurrentStatus.Ahead;
            ActiveRepository.BehindCount = CurrentStatus.Behind;

            // Update staged / unstaged lists
            StagedChanges.Clear();
            UnstagedChanges.Clear();

            foreach (var chg in CurrentStatus.Changes)
            {
                var vm = new GitChangeItemViewModel(chg);
                if (chg.IsStaged)
                {
                    StagedChanges.Add(vm);
                }
                else
                {
                    UnstagedChanges.Add(vm);
                }
            }

            // Load Commits
            var commitsRes = await _gitService.GetCommitsAsync(ActiveRepository.Path, 50);
            if (commitsRes.Success)
            {
                Commits.Clear();
                var lines = commitsRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var p = line.Split('\t');
                    if (p.Length >= 7)
                    {
                        Commits.Add(new GitCommit
                        {
                            Hash = p[0].Trim(),
                            AuthorName = p[2].Trim(),
                            AuthorEmail = p[3].Trim(),
                            Date = DateTime.TryParse(p[4].Trim(), out var dt) ? dt : DateTime.UtcNow,
                            RelativeTime = p[5].Trim(),
                            Subject = p[6].Trim(),
                            Body = p.Length > 7 ? p[7].Trim() : string.Empty
                        });
                    }
                }
                if (Commits.Count > 0)
                {
                    SelectedCommit = Commits[0];
                }
            }

            // Load Branches
            var branchesRes = await _gitService.GetBranchesAsync(ActiveRepository.Path);
            if (branchesRes.Success)
            {
                Branches.Clear();
                var bLines = branchesRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in bLines)
                {
                    var p = line.Split('\t');
                    if (p.Length >= 5)
                    {
                        var name = p[0].Trim();
                        bool isHead = p[1].Trim() == "*";
                        bool isRemote = name.StartsWith("remotes/") || name.StartsWith("origin/");
                        Branches.Add(new GitBranch
                        {
                            Name = name,
                            IsCurrent = isHead,
                            IsRemote = isRemote,
                            Upstream = p[2].Trim(),
                            LatestCommitHash = p[3].Trim(),
                            LatestCommitSubject = p[4].Trim()
                        });
                    }
                }
            }

            // Load Stashes
            var stashRes = await _gitService.GetStashesAsync(ActiveRepository.Path);
            if (stashRes.Success)
            {
                Stashes.Clear();
                var sLines = stashRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in sLines)
                {
                    var p = line.Split('\t');
                    if (p.Length >= 3)
                    {
                        var rawIndex = p[0].Trim().Replace("stash@{", "").Replace("}", "");
                        Stashes.Add(new GitStash
                        {
                            Index = int.TryParse(rawIndex, out var idx) ? idx : 0,
                            Message = p[1].Trim(),
                            Date = p[2].Trim()
                        });
                    }
                }
            }

            // Load Remotes
            var remoteRes = await _gitService.GetRemotesAsync(ActiveRepository.Path);
            if (remoteRes.Success)
            {
                Remotes.Clear();
                var rLines = remoteRes.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var dict = new Dictionary<string, (string Fetch, string Push)>();
                foreach (var line in rLines)
                {
                    var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        var name = parts[0].Trim();
                        var url = parts[1].Trim();
                        var type = parts[2].Trim();

                        if (!dict.ContainsKey(name)) dict[name] = (string.Empty, string.Empty);
                        if (type.Contains("(fetch)")) dict[name] = (url, dict[name].Push);
                        if (type.Contains("(push)")) dict[name] = (dict[name].Fetch, url);
                    }
                }
                foreach (var kvp in dict)
                {
                    Remotes.Add(new GitRemote
                    {
                        Name = kvp.Key,
                        FetchUrl = kvp.Value.Fetch,
                        PushUrl = kvp.Value.Push
                    });
                }
            }
        }
        catch (Exception ex)
        {
            SetNotification($"Error loading repo details: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
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
            var diffRes = await _gitService.DiffAsync(ActiveRepository.Path, SelectedChange.IsStaged, SelectedChange.Model.Path);
            RawDiffText = diffRes.Output;

            DiffLines.Clear();
            if (!string.IsNullOrEmpty(RawDiffText))
            {
                var lines = RawDiffText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                int oldLine = 0;
                int newLine = 0;

                foreach (var line in lines)
                {
                    var item = new DiffLineItem { Text = line };

                    if (line.StartsWith("@@"))
                    {
                        item.LineType = "header";
                        // Parse hunk header @@ -old,len +new,len @@
                        var match = System.Text.RegularExpressions.Regex.Match(line, @"@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@");
                        if (match.Success)
                        {
                            oldLine = int.Parse(match.Groups[1].Value);
                            newLine = int.Parse(match.Groups[2].Value);
                        }
                    }
                    else if (line.StartsWith("+") && !line.StartsWith("+++"))
                    {
                        item.LineType = "addition";
                        item.NewLineNumber = newLine++;
                    }
                    else if (line.StartsWith("-") && !line.StartsWith("---"))
                    {
                        item.LineType = "deletion";
                        item.OldLineNumber = oldLine++;
                    }
                    else if (!line.StartsWith("diff --git") && !line.StartsWith("index ") && !line.StartsWith("---") && !line.StartsWith("+++"))
                    {
                        item.LineType = "context";
                        item.OldLineNumber = oldLine++;
                        item.NewLineNumber = newLine++;
                    }
                    else
                    {
                        item.LineType = "meta";
                    }

                    DiffLines.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            RawDiffText = $"Error reading diff: {ex.Message}";
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
            var res = await _processService.ExecuteAsync("git", new[] { "show", SelectedCommit.Hash, "--stat", "-p" }, ActiveRepository.Path);
            CommitDiffText = res.Success ? res.StandardOutput : res.StandardError;
        }
        catch (Exception ex)
        {
            CommitDiffText = $"Error loading commit details: {ex.Message}";
        }
    }

    public async Task StageAllAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.StageAsync(ActiveRepository.Path, new[] { "." });
        if (res.Success)
        {
            SetNotification("Staged all changes.", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Stage failed: {res.Error}", "error");
        }
    }

    public async Task UnstageAllAsync()
    {
        if (ActiveRepository == null) return;
        var res = await _gitService.UnstageAsync(ActiveRepository.Path, new[] { "." });
        if (res.Success)
        {
            SetNotification("Unstaged all changes.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Unstage failed: {res.Error}", "error");
        }
    }

    public async Task StageItemAsync(GitChangeItemViewModel? item)
    {
        if (ActiveRepository == null || item == null) return;
        var res = await _gitService.StageAsync(ActiveRepository.Path, new[] { item.Model.Path });
        if (res.Success)
        {
            await LoadActiveRepoDetailsAsync();
        }
    }

    public async Task UnstageItemAsync(GitChangeItemViewModel? item)
    {
        if (ActiveRepository == null || item == null) return;
        var res = await _gitService.UnstageAsync(ActiveRepository.Path, new[] { item.Model.Path });
        if (res.Success)
        {
            await LoadActiveRepoDetailsAsync();
        }
    }

    public async Task DiscardItemAsync(GitChangeItemViewModel? item)
    {
        if (ActiveRepository == null || item == null) return;
        var confirm = MessageBox.Show($"Are you sure you want to discard changes in '{item.DisplayName}'?", "Confirm Discard", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await _gitService.DiscardAsync(ActiveRepository.Path, new[] { item.Model.Path });
        if (res.Success)
        {
            SetNotification($"Discarded changes in {item.DisplayName}.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Discard error: {res.Error}", "error");
        }
    }

    public async Task DiscardSelectedAsync()
    {
        if (ActiveRepository == null) return;
        var selected = UnstagedChanges.Where(x => x.IsSelected).Select(x => x.Model.Path).ToList();
        if (selected.Count == 0)
        {
            SetNotification("No unstaged files selected for discard.", "warning");
            return;
        }

        var confirm = MessageBox.Show($"Discard changes in {selected.Count} selected files?", "Confirm Discard Selected", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await _gitService.DiscardAsync(ActiveRepository.Path, selected);
        if (res.Success)
        {
            SetNotification($"Discarded changes in {selected.Count} files.", "info");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Discard error: {res.Error}", "error");
        }
    }

    public async Task CommitAsync()
    {
        if (ActiveRepository == null) return;
        if (string.IsNullOrWhiteSpace(CommitSubject))
        {
            SetNotification("Commit subject cannot be empty.", "warning");
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
            SetNotification("Commit created successfully!", "success");
            await LoadActiveRepoDetailsAsync();
        }
        else
        {
            SetNotification($"Commit failed: {res.Error}", "error");
        }
    }

    public async Task CommitAndPushAsync()
    {
        if (ActiveRepository == null) return;
        await CommitAsync();
        await PushAsync();
    }

    public async Task FetchAsync()
    {
        if (ActiveRepository == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = "Fetching remote references...";
            var res = await _gitService.FetchAsync(ActiveRepository.Path);
            if (res.Success)
            {
                SetNotification("Fetch completed successfully.", "success");
                await LoadActiveRepoDetailsAsync();
            }
            else
            {
                SetNotification($"Fetch failed: {res.Error}", "error");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PullAsync()
    {
        if (ActiveRepository == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = "Pulling latest changes from upstream...";
            var res = await _gitService.PullAsync(ActiveRepository.Path);
            if (res.Success)
            {
                SetNotification("Pull completed successfully!", "success");
                await LoadActiveRepoDetailsAsync();
            }
            else
            {
                SetNotification($"Pull failed: {res.Error}", "error");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PushAsync()
    {
        if (ActiveRepository == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = "Pushing commits to remote...";
            var res = await _gitService.PushAsync(ActiveRepository.Path);
            if (res.Success)
            {
                SetNotification("Push succeeded!", "success");
                await LoadActiveRepoDetailsAsync();
            }
            else
            {
                SetNotification($"Push failed: {res.Error}", "error");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckoutBranchAsync(string? branchName)
    {
        if (ActiveRepository == null || string.IsNullOrWhiteSpace(branchName)) return;
        var cleanBranch = branchName.StartsWith("remotes/origin/")
            ? branchName["remotes/origin/".Length..]
            : branchName;

        var res = await _gitService.CheckoutAsync(ActiveRepository.Path, cleanBranch);
        if (res.Success)
        {
            SetNotification($"Switched to branch '{cleanBranch}'.", "success");
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

    // GitHub Integration Logic
    public async Task LoadGitHubAccountsAsync()
    {
        try
        {
            var accounts = await _githubCliService.GetAccountsAsync();
            GitHubAccounts.Clear();
            foreach (var acc in accounts)
            {
                GitHubAccounts.Add(acc);
            }
            SelectedGitHubAccount = GitHubAccounts.FirstOrDefault(a => a.IsActive) ?? GitHubAccounts.FirstOrDefault();
            GitHubCliStatus = accounts.Count > 0 ? $"{accounts.Count} accounts authenticated." : "No accounts authenticated in gh CLI.";
        }
        catch (Exception ex)
        {
            GitHubCliStatus = $"gh CLI error: {ex.Message}";
        }
    }

    public async Task LoadGitHubRepositoriesAsync()
    {
        if (SelectedGitHubAccount == null)
        {
            GitHubRepositories.Clear();
            FilteredGitHubRepositories.Clear();
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = $"Loading repositories for @{SelectedGitHubAccount.Username}...";
            var repos = await _githubCliService.ListRepositoriesAsync(SelectedGitHubAccount.Host, SelectedGitHubAccount.Username);
            GitHubRepositories.Clear();
            foreach (var r in repos)
            {
                GitHubRepositories.Add(r);
            }
            ApplyGitHubFilter();
            SetNotification($"Loaded {repos.Count} repositories for @{SelectedGitHubAccount.Username}.", "success");
        }
        catch (Exception ex)
        {
            SetNotification($"Failed to load repositories: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyGitHubFilter()
    {
        FilteredGitHubRepositories.Clear();
        var q = (GitHubSearchFilter ?? string.Empty).Trim();
        var list = string.IsNullOrEmpty(q)
            ? GitHubRepositories
            : new ObservableCollection<GitHubRepositoryInfo>(GitHubRepositories.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Owner.Contains(q, StringComparison.OrdinalIgnoreCase)));

        foreach (var r in list)
        {
            FilteredGitHubRepositories.Add(r);
        }
    }

    public async Task SwitchGitHubAccountAsync(GitHubAccountInfo? account)
    {
        if (account == null) return;
        try
        {
            IsBusy = true;
            BusyMessage = $"Switching active account to @{account.Username}...";
            await _githubCliService.SwitchAccountAsync(account.Host, account.Username);
            SetNotification($"Active account switched to @{account.Username}.", "success");
            await LoadGitHubAccountsAsync();
        }
        catch (Exception ex)
        {
            SetNotification($"Account switch error: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CloneGitHubRepoAsync(GitHubRepositoryInfo? repo)
    {
        if (repo == null) return;
        if (SelectedGitHubAccount == null)
        {
            SetNotification("Please select an authenticated GitHub account first.", "warning");
            return;
        }

        var defaultDest = Path.Combine(WorkspacePath, repo.Name);
        var res = MessageBox.Show($"Clone '{repo.Name}' into:\n{defaultDest}\n\nProceed?", "Clone Repository", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            BusyMessage = $"Cloning {repo.Name}...";
            var cloneResult = await _githubCliService.ExecuteForAccountAsync(
                SelectedGitHubAccount.Host,
                SelectedGitHubAccount.Username,
                token => _gitService.CloneAsync(repo.Url, defaultDest, token));

            if (cloneResult.Success)
            {
                SetNotification($"Cloned {repo.Name} successfully!", "success");
                await ScanWorkspaceAsync();
            }
            else
            {
                SetNotification($"Clone failed: {cloneResult.Error}", "error");
            }
        }
        catch (Exception ex)
        {
            SetNotification($"Clone error: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SetupGitCredentialsAsync()
    {
        try
        {
            IsBusy = true;
            BusyMessage = "Configuring Git credentials (gh auth setup-git)...";
            var result = await _githubCliService.SetupGitCredentialsAsync();
            if (result.Success)
            {
                SetNotification("Configured Git to authenticate through GitHub CLI!", "success");
            }
            else
            {
                SetNotification($"Setup Git failed: {result.Error}", "error");
            }
        }
        catch (Exception ex)
        {
            SetNotification($"Setup Git error: {ex.Message}", "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
    }

    private void CopyToClipboard(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
            SetNotification("Copied to clipboard!", "info");
        }
    }

    private void SetNotification(string message, string severity = "info")
    {
        StatusMessage = message;
        StatusSeverity = severity;
    }

    // AI Implementation Methods
    private void InitializeAiProvidersAndModels()
    {
        if (_aiManager != null)
        {
            var providers = _aiManager.GetAllProviders().Select(p => p.ProviderName).ToList();
            if (providers.Count == 0)
            {
                providers.Add("Gemini");
                providers.Add("DeepSeek");
            }
            AvailableAiProviders = new ObservableCollection<string>(providers);
        }
        else
        {
            AvailableAiProviders = new ObservableCollection<string> { "Gemini", "DeepSeek" };
        }

        UpdateAiModelsForProvider(_selectedAiProvider);
    }

    private void UpdateAiModelsForProvider(string provider)
    {
        var models = new List<string>();
        if (_aiManager != null)
        {
            models = _aiManager.GetSupportedModels(provider).ToList();
        }

        if (models.Count == 0)
        {
            if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
            {
                models = new List<string>
                {
                    "gemini-3.5-flash-lite",
                    "gemini-flash-lite-latest",
                    "gemini-3.8-flash",
                    "gemini-3.5-flash",
                    "gemini-3.7-flash",
                    "gemini-3.6-flash",
                    "gemini-3.1-flash-lite",
                    "gemini-flash-latest",
                    "gemini-pro-latest",
                    "gemini-3.1-pro-preview",
                    "gemini-4-argon",
                    "gemini-2.0-flash",
                    "gemini-1.5-flash",
                    "gemini-1.5-pro"
                };
            }
            else
            {
                models = new List<string> { "deepseek-chat", "deepseek-reasoner" };
            }
        }

        AvailableAiModels = new ObservableCollection<string>(models);
        if (!AvailableAiModels.Contains(SelectedAiModel))
        {
            SelectedAiModel = AvailableAiModels.FirstOrDefault() ?? string.Empty;
        }
    }

    public async Task AutoGenerateCommitAsync()
    {
        if (ActiveRepository == null)
        {
            AiStatusMessage = "Please select a repository first.";
            AiStatusSeverity = "warning";
            return;
        }

        if (_aiManager == null)
        {
            AiStatusMessage = "AI Commit Service is not initialized.";
            AiStatusSeverity = "error";
            return;
        }

        IsAiGenerating = true;
        AiStatusMessage = $"Analyzing Git diff & requesting commit from {SelectedAiProvider} ({SelectedAiModel})...";
        AiStatusSeverity = "info";

        try
        {
            var result = await _aiManager.GenerateCommitMessageAsync(
                ActiveRepository.Path,
                SelectedAiProvider,
                SelectedAiModel,
                includeUnstaged: true);

            if (result.Success)
            {
                CommitSubject = result.Title ?? string.Empty;
                CommitDescription = result.Description ?? string.Empty;

                if (result.UsedFallback)
                {
                    AiStatusSeverity = "warning";
                    AiStatusMessage = $"Generated via local heuristic fallback: {result.Warning ?? result.Error}";
                }
                else
                {
                    AiStatusSeverity = "success";
                    var truncatedNotice = result.DiffTruncated ? " [Diff truncated for token limits]" : "";
                    AiStatusMessage = $"✓ Generated via {result.Provider} ({result.Model}) in {result.DurationMs}ms{truncatedNotice}";
                }
            }
            else
            {
                AiStatusSeverity = "error";
                AiStatusMessage = $"AI Generation Error: {result.Error ?? "Could not generate commit message."}";
            }
        }
        catch (Exception ex)
        {
            AiStatusSeverity = "error";
            AiStatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsAiGenerating = false;
        }
    }

    public async Task TestAiConnectionAsync(string? provider)
    {
        var targetProvider = string.IsNullOrWhiteSpace(provider) ? SelectedAiProvider : provider;

        if (_aiManager == null)
        {
            AiTestStatus = "AI manager service not available.";
            AiTestSeverity = "error";
            return;
        }

        IsAiTesting = true;
        AiTestStatus = $"Connecting to {targetProvider} API using model '{SelectedAiModel}'...";
        AiTestSeverity = "info";

        try
        {
            var (success, message) = await _aiManager.TestConnectionAsync(targetProvider, SelectedAiModel);
            AiTestStatus = message;
            AiTestSeverity = success ? "success" : "error";
        }
        catch (Exception ex)
        {
            AiTestStatus = $"Test failed: {ex.Message}";
            AiTestSeverity = "error";
        }
        finally
        {
            IsAiTesting = false;
        }
    }

    public void SaveAiSettings()
    {
        try
        {
            var settings = _settingsService.LoadSettings();
            settings.ActiveAiProvider = SelectedAiProvider;
            settings.SelectedAiModel = SelectedAiModel;
            settings.GeminiApiKey = GeminiApiKeyInput;
            settings.DeepSeekApiKey = DeepSeekApiKeyInput;
            _settingsService.SaveSettings(settings);

            // Update in-memory options if instances present
            if (_geminiOptions != null) _geminiOptions.ApiKey = GeminiApiKeyInput;
            if (_deepSeekOptions != null) _deepSeekOptions.ApiKey = DeepSeekApiKeyInput;

            // Also notify concrete service instances if available
            var geminiService = _aiManager?.GetProvider("Gemini") as GeminiCommitService;
            geminiService?.UpdateApiKey(GeminiApiKeyInput);

            var deepSeekService = _aiManager?.GetProvider("DeepSeek") as DeepSeekCommitService;
            deepSeekService?.UpdateApiKey(DeepSeekApiKeyInput);

            AiTestStatus = "✓ AI settings saved successfully.";
            AiTestSeverity = "success";
            SetNotification("AI settings saved successfully.", "success");
        }
        catch (Exception ex)
        {
            AiTestStatus = $"Save error: {ex.Message}";
            AiTestSeverity = "error";
        }
    }
}
