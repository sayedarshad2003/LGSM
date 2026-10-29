using System.IO;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Windows;
using LGMS.Desktop.ViewModels;
using LGMS.Models;
using LGMS.Services;

namespace LGMS.Desktop.Services;

public class VoiceStepItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, running, completed, failed, warning
    public string? Output { get; set; }
}

public class VoiceExecutionResult
{
    public bool Success { get; set; }
    public string Intent { get; set; } = string.Empty;
    public Repository? Repository { get; set; }
    public List<VoiceStepItem> Steps { get; set; } = new();
    public string SpokenReply { get; set; } = string.Empty;
    public string? RawOutput { get; set; }
}

public class VoiceAssistantService : IDisposable
{
    private readonly MainViewModel _mainViewModel;
    private readonly IGitService _gitService;
    private readonly IProcessService _processService;
    private readonly IAICommitManager? _aiManager;

    private SpeechSynthesizer? _synthesizer;
    private SpeechRecognitionEngine? _recognizer;
    private bool _isListening;
    private bool _isMuted;

    public event Action<string>? SpeechRecognizedPartial;
    public event Action<string>? SpeechRecognizedFinal;
    public event Action<string>? StatusChanged;
    public event Action<bool>? ListeningStateChanged;
    public event Action<bool>? SpeakingStateChanged;

    public bool IsListening => _isListening;
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            if (_isMuted) StopSpeaking();
        }
    }

    public VoiceAssistantService(
        MainViewModel mainViewModel,
        IGitService gitService,
        IProcessService processService,
        IAICommitManager? aiManager = null)
    {
        _mainViewModel = mainViewModel;
        _gitService = gitService;
        _processService = processService;
        _aiManager = aiManager;

        InitializeSpeech();
    }

    private void InitializeSpeech()
    {
        try
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SpeakStarted += (s, e) => SpeakingStateChanged?.Invoke(true);
            _synthesizer.SpeakCompleted += (s, e) => SpeakingStateChanged?.Invoke(false);

            // Select preferred voice if available
            var installedVoices = _synthesizer.GetInstalledVoices();
            var englishVoice = installedVoices.FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.Name.StartsWith("en"));
            if (englishVoice != null)
            {
                _synthesizer.SelectVoice(englishVoice.VoiceInfo.Name);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to init SpeechSynthesizer: {ex.Message}");
        }

        try
        {
            _recognizer = new SpeechRecognitionEngine();
            _recognizer.SetInputToDefaultAudioDevice();

            // Load dictation grammar for natural speech
            var dictationGrammar = new DictationGrammar();
            _recognizer.LoadGrammar(dictationGrammar);

            _recognizer.SpeechHypothesized += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    SpeechRecognizedPartial?.Invoke(e.Result.Text);
                }
            };

            _recognizer.SpeechRecognized += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text) && e.Result.Confidence >= 0.35f)
                {
                    SpeechRecognizedFinal?.Invoke(e.Result.Text);
                }
            };

            _recognizer.RecognizeCompleted += (s, e) =>
            {
                _isListening = false;
                ListeningStateChanged?.Invoke(false);
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to init SpeechRecognitionEngine: {ex.Message}");
        }
    }

    public void StartListening()
    {
        if (_recognizer == null)
        {
            StatusChanged?.Invoke("Speech recognition not available on this machine.");
            return;
        }

        try
        {
            StopSpeaking();
            _isListening = true;
            ListeningStateChanged?.Invoke(true);
            StatusChanged?.Invoke("Listening... speak your command");
            _recognizer.RecognizeAsync(RecognizeMode.Single);
        }
        catch (Exception ex)
        {
            _isListening = false;
            ListeningStateChanged?.Invoke(false);
            StatusChanged?.Invoke($"Microphone error: {ex.Message}");
        }
    }

    public void StopListening()
    {
        if (_recognizer != null && _isListening)
        {
            try
            {
                _recognizer.RecognizeAsyncStop();
            }
            catch { }
        }
        _isListening = false;
        ListeningStateChanged?.Invoke(false);
        StatusChanged?.Invoke("Ready");
    }

    public void Speak(string text)
    {
        if (_isMuted || _synthesizer == null || string.IsNullOrWhiteSpace(text)) return;

        try
        {
            StopSpeaking();
            _synthesizer.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpeakAsync error: {ex.Message}");
        }
    }

    public void StopSpeaking()
    {
        try
        {
            _synthesizer?.SpeakAsyncCancelAll();
            SpeakingStateChanged?.Invoke(false);
        }
        catch { }
    }

    public async Task<VoiceExecutionResult> ProcessCommandAsync(string rawCommand, Repository? overrideRepo = null)
    {
        var steps = new List<VoiceStepItem>();
        string commandText = rawCommand.Trim();
        string normalizedCmd = Regex.Replace(commandText.ToLowerInvariant(), @"[^\w\s-]", " ");

        var repos = _mainViewModel.AllRepositories.ToList();
        Repository? targetRepo = overrideRepo ?? ResolveTargetRepository(normalizedCmd, repos);

        // 1. List Repositories
        if (IsListRepositoriesCommand(normalizedCmd))
        {
            steps.Add(new VoiceStepItem { Title = "Repository Catalog", Description = "Scanning all workspace repositories.", Status = "completed" });
            var repoNames = repos.Select(r => r.Name).ToList();
            string spoken = repoNames.Count > 0
                ? $"I found {repoNames.Count} repositories: {string.Join(", ", repoNames)}. Which one would you like to work with?"
                : "No repositories discovered in current workspace.";

            return new VoiceExecutionResult
            {
                Success = true,
                Intent = "list_repositories",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = string.Join("\n", repos.Select(r => $"{r.Name} ({r.Path}) [{r.CurrentBranch}]"))
            };
        }

        // 2. Select Repository
        if (IsSwitchRepositoryCommand(normalizedCmd) || (targetRepo != null && IsStrictRepoSelection(normalizedCmd, targetRepo.Name)))
        {
            if (targetRepo == null)
            {
                return new VoiceExecutionResult
                {
                    Success = false,
                    Intent = "select_repository",
                    Steps = new List<VoiceStepItem> { new() { Title = "Repository Lookup", Description = "Repository not found.", Status = "failed" } },
                    SpokenReply = "I could not find a repository matching that name. Available repositories are: " + string.Join(", ", repos.Select(r => r.Name))
                };
            }

            // Switch active repo in MainViewModel on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainViewModel.ActiveRepository = targetRepo;
            });

            steps.Add(new VoiceStepItem { Title = "Repository Selected", Description = $"Switched active workspace context to '{targetRepo.Name}'", Status = "completed" });
            steps.Add(new VoiceStepItem { Title = "Status Inspection", Description = "Reading repository branch and uncommitted changes...", Status = "completed" });

            var status = await _gitService.ParseStatusAsync(targetRepo.Path);
            string cleanStatus = status.IsClean
                ? "The repository is clean."
                : $"There are {status.Changes.Count} uncommitted changes.";

            string spoken = $"Selected repository {targetRepo.Name}. Active branch is {status.CurrentBranch ?? targetRepo.CurrentBranch ?? "master"}. {cleanStatus} What would you like to do?";

            return new VoiceExecutionResult
            {
                Success = true,
                Intent = "select_repository",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = $"Active: {targetRepo.Name}\nPath: {targetRepo.Path}\nBranch: {status.CurrentBranch}\nClean: {status.IsClean}"
            };
        }

        // Fallback to currently active repository in MainViewModel
        targetRepo ??= _mainViewModel.ActiveRepository ?? repos.FirstOrDefault();

        if (targetRepo == null)
        {
            return new VoiceExecutionResult
            {
                Success = false,
                Intent = "need_repo",
                Steps = new List<VoiceStepItem> { new() { Title = "Repository Context", Description = "No active repository selected.", Status = "failed" } },
                SpokenReply = "Please specify a repository first. For example say: 'Select repository " + (repos.FirstOrDefault()?.Name ?? "LGMS") + "'."
            };
        }

        // Switch to target repo if different
        if (_mainViewModel.ActiveRepository != targetRepo)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainViewModel.ActiveRepository = targetRepo;
            });
        }

        steps.Add(new VoiceStepItem { Title = "Context Active", Description = $"Target repository: {targetRepo.Name}", Status = "completed" });

        // 3. Git Status
        if (normalizedCmd.Contains("status") || normalizedCmd.Contains("what changed") || normalizedCmd.Contains("show changes") || normalizedCmd.Contains("dirty"))
        {
            steps.Add(new VoiceStepItem { Title = "Git Status", Description = $"Reading status for {targetRepo.Name}...", Status = "running" });
            var status = await _gitService.ParseStatusAsync(targetRepo.Path);
            steps[^1].Status = "completed";

            string spoken = status.IsClean
                ? $"Repository {targetRepo.Name} on branch {status.CurrentBranch} is clean with no uncommitted changes."
                : $"Repository {targetRepo.Name} has {status.Changes.Count} uncommitted changes on branch {status.CurrentBranch}. {status.StagedChanges.Count} staged, {status.UnstagedChanges.Count} unstaged.";

            return new VoiceExecutionResult
            {
                Success = true,
                Intent = "status",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = string.Join("\n", status.Changes.Select(c => $"{(c.IsStaged ? "[STAGED]" : "[UNSTAGED]")} {c.Status}: {c.Path}"))
            };
        }

        // 4. Git Pull
        if (normalizedCmd.Contains("pull") || normalizedCmd.Contains("sync") || normalizedCmd.Contains("update from remote"))
        {
            steps.Add(new VoiceStepItem { Title = "Git Pull", Description = $"Pulling changes from remote into {targetRepo.Name}...", Status = "running" });
            var result = await _gitService.PullAsync(targetRepo.Path);
            steps[^1].Status = result.Success ? "completed" : "failed";
            steps[^1].Output = result.Output ?? result.Error;

            // Trigger refresh in MainViewModel
            Application.Current.Dispatcher.Invoke(() => _mainViewModel.RefreshCommand.Execute(null));

            string spoken = result.Success
                ? (result.Output?.Contains("Already up to date", StringComparison.OrdinalIgnoreCase) == true
                    ? $"Pulled latest changes for {targetRepo.Name}. Already up to date."
                    : $"Successfully pulled latest changes into {targetRepo.Name}.")
                : $"Pull failed for {targetRepo.Name}: {result.Error}";

            return new VoiceExecutionResult
            {
                Success = result.Success,
                Intent = "pull",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = result.Output ?? result.Error
            };
        }

        // 5. Git Fetch
        if (normalizedCmd.Contains("fetch"))
        {
            steps.Add(new VoiceStepItem { Title = "Git Fetch", Description = $"Fetching remote for {targetRepo.Name}...", Status = "running" });
            var result = await _gitService.FetchAsync(targetRepo.Path);
            steps[^1].Status = result.Success ? "completed" : "failed";

            string spoken = result.Success
                ? $"Successfully fetched all remotes for {targetRepo.Name}."
                : $"Fetch failed: {result.Error}";

            return new VoiceExecutionResult
            {
                Success = result.Success,
                Intent = "fetch",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = result.Output ?? result.Error
            };
        }

        // 6. Git Push
        if (normalizedCmd.Contains("push"))
        {
            steps.Add(new VoiceStepItem { Title = "Git Push", Description = $"Pushing commits to remote origin for {targetRepo.Name}...", Status = "running" });
            var result = await _gitService.PushAsync(targetRepo.Path);
            steps[^1].Status = result.Success ? "completed" : "failed";

            string spoken = result.Success
                ? $"Pushed all commits to remote successfully for {targetRepo.Name}."
                : $"Push failed: {result.Error}";

            return new VoiceExecutionResult
            {
                Success = result.Success,
                Intent = "push",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = result.Output ?? result.Error
            };
        }

        // 7. Create Branch
        var createBranchMatch = Regex.Match(normalizedCmd, @"(?:create|make|new)\s+branch\s+([a-zA-Z0-9_\-\.\/]+)");
        if (createBranchMatch.Success)
        {
            string newBranch = createBranchMatch.Groups[1].Value.Trim();
            steps.Add(new VoiceStepItem { Title = "Create Branch", Description = $"Creating branch '{newBranch}' in {targetRepo.Name}...", Status = "running" });
            var result = await _gitService.CreateBranchAsync(targetRepo.Path, newBranch);
            steps[^1].Status = result.Success ? "completed" : "failed";

            Application.Current.Dispatcher.Invoke(() => _mainViewModel.RefreshCommand.Execute(null));

            string spoken = result.Success
                ? $"Created and checked out new branch {newBranch} in {targetRepo.Name}."
                : $"Failed to create branch: {result.Error}";

            return new VoiceExecutionResult
            {
                Success = result.Success,
                Intent = "create_branch",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = result.Output ?? result.Error
            };
        }

        // 8. Checkout Branch
        var checkoutMatch = Regex.Match(normalizedCmd, @"(?:checkout|switch to|switch)\s+(?:branch\s+)?([a-zA-Z0-9_\-\.\/]+)");
        if (checkoutMatch.Success)
        {
            string branchName = checkoutMatch.Groups[1].Value.Trim();
            steps.Add(new VoiceStepItem { Title = "Checkout Branch", Description = $"Switching to branch '{branchName}'...", Status = "running" });
            var result = await _gitService.CheckoutAsync(targetRepo.Path, branchName);
            steps[^1].Status = result.Success ? "completed" : "failed";

            Application.Current.Dispatcher.Invoke(() => _mainViewModel.RefreshCommand.Execute(null));

            string spoken = result.Success
                ? $"Switched to branch {branchName} in {targetRepo.Name}."
                : $"Failed to checkout {branchName}: {result.Error}";

            return new VoiceExecutionResult
            {
                Success = result.Success,
                Intent = "checkout_branch",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = spoken,
                RawOutput = result.Output ?? result.Error
            };
        }

        // 9. AI Commit Generation
        if (normalizedCmd.Contains("ai commit") || normalizedCmd.Contains("generate commit") || normalizedCmd.Contains("suggest commit"))
        {
            steps.Add(new VoiceStepItem { Title = "AI Commit Generation", Description = $"Analyzing diff in {targetRepo.Name}...", Status = "running" });
            if (_aiManager != null)
            {
                try
                {
                    var aiResult = await _aiManager.GenerateCommitMessageAsync(targetRepo.Path, includeUnstaged: true);
                    steps[^1].Status = aiResult.Success ? "completed" : "failed";

                    string spoken = aiResult.Success
                        ? $"Generated AI commit message: \"{aiResult.Title}\"."
                        : $"Could not generate commit message: {aiResult.Error}";

                    return new VoiceExecutionResult
                    {
                        Success = aiResult.Success,
                        Intent = "ai_commit",
                        Repository = targetRepo,
                        Steps = steps,
                        SpokenReply = spoken,
                        RawOutput = aiResult.FullMessage
                    };
                }
                catch (Exception ex)
                {
                    steps[^1].Status = "failed";
                    return new VoiceExecutionResult
                    {
                        Success = false,
                        Intent = "ai_commit",
                        Steps = steps,
                        SpokenReply = $"Failed to generate AI commit: {ex.Message}"
                    };
                }
            }
        }

        // 10. Open in VS Code
        if (normalizedCmd.Contains("code") || normalizedCmd.Contains("editor"))
        {
            steps.Add(new VoiceStepItem { Title = "Launch VS Code", Description = $"Opening {targetRepo.Path} in VS Code...", Status = "completed" });
            await _processService.ExecuteAsync("code", new[] { targetRepo.Path }, targetRepo.Path);

            return new VoiceExecutionResult
            {
                Success = true,
                Intent = "open_code",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = $"Opening repository {targetRepo.Name} in Visual Studio Code."
            };
        }

        // 11. Open in Explorer
        if (normalizedCmd.Contains("explorer") || normalizedCmd.Contains("open folder"))
        {
            steps.Add(new VoiceStepItem { Title = "Open Explorer", Description = $"Opening directory in Windows Explorer...", Status = "completed" });
            await _processService.ExecuteAsync("explorer.exe", new[] { targetRepo.Path }, targetRepo.Path);

            return new VoiceExecutionResult
            {
                Success = true,
                Intent = "open_explorer",
                Repository = targetRepo,
                Steps = steps,
                SpokenReply = $"Opening folder for {targetRepo.Name} in Windows Explorer."
            };
        }

        // Fallback / Unknown
        steps.Add(new VoiceStepItem { Title = "Command Analysis", Description = $"Action '{commandText}' under {targetRepo.Name} not recognized.", Status = "warning" });
        return new VoiceExecutionResult
        {
            Success = false,
            Intent = "unknown",
            Repository = targetRepo,
            Steps = steps,
            SpokenReply = $"I recognized repository {targetRepo.Name}, but not the action. Try saying 'Status', 'Pull', 'Fetch', 'Push', 'Checkout main', or 'List repositories'."
        };
    }

    private static Repository? ResolveTargetRepository(string normalizedCmd, List<Repository> repos)
    {
        foreach (var repo in repos.OrderByDescending(r => r.Name.Length))
        {
            string cleanRepoName = Regex.Replace(repo.Name.ToLowerInvariant(), @"[^\w\s-]", " ").Trim();
            if (normalizedCmd.Contains(cleanRepoName)) return repo;

            string squishedRepo = cleanRepoName.Replace("_", "").Replace("-", "").Replace(" ", "");
            string squishedCmd = normalizedCmd.Replace("_", "").Replace("-", "").Replace(" ", "");
            if (squishedCmd.Contains(squishedRepo) && squishedRepo.Length >= 3) return repo;
        }
        return null;
    }

    private static bool IsListRepositoriesCommand(string cmd)
    {
        return cmd.Contains("list repo") || cmd.Contains("show repositories") || cmd.Contains("what repositories") || cmd.Contains("what repos");
    }

    private static bool IsSwitchRepositoryCommand(string cmd)
    {
        return cmd.StartsWith("select repo") || cmd.StartsWith("switch repo") || cmd.StartsWith("switch to repo") || cmd.StartsWith("go to repo");
    }

    private static bool IsStrictRepoSelection(string cmd, string repoName)
    {
        var cleaned = cmd.Replace("select", "").Replace("switch to", "").Replace("switch", "").Replace("go to", "").Replace("repository", "").Replace("repo", "").Trim();
        return cleaned.Equals(repoName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            StopListening();
            _recognizer?.Dispose();
            _synthesizer?.Dispose();
        }
        catch { }
    }
}
