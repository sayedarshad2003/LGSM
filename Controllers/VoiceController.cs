using System.Diagnostics;
using System.Text.RegularExpressions;
using LGMS.Data;
using LGMS.Models;
using LGMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VoiceController : Controller
{
    private readonly IRepositoryScanner _scanner;
    private readonly IGitService _gitService;
    private readonly IProcessService _processService;
    private readonly IAICommitManager _aiManager;
    private readonly LgmsDbContext _db;
    private readonly ILogger<VoiceController> _logger;

    public VoiceController(
        IRepositoryScanner scanner,
        IGitService gitService,
        IProcessService processService,
        IAICommitManager aiManager,
        LgmsDbContext db,
        ILogger<VoiceController> logger)
    {
        _scanner = scanner;
        _gitService = gitService;
        _processService = processService;
        _aiManager = aiManager;
        _db = db;
        _logger = logger;
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext([FromQuery] string? folder)
    {
        try
        {
            var workingFolder = folder ?? HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
            var repos = await _scanner.ScanAsync(workingFolder);

            var activeRepoPath = HttpContext.Session.GetString("ActiveRepoPath");
            Repository? activeRepo = null;

            if (!string.IsNullOrWhiteSpace(activeRepoPath))
            {
                activeRepo = repos.FirstOrDefault(r => string.Equals(r.Path, activeRepoPath, StringComparison.OrdinalIgnoreCase));
            }

            if (activeRepo == null && repos.Any())
            {
                activeRepo = repos.First();
                HttpContext.Session.SetString("ActiveRepoPath", activeRepo.Path);
            }

            return Ok(new
            {
                success = true,
                workingFolder,
                activeRepo = activeRepo != null ? new
                {
                    activeRepo.Id,
                    activeRepo.Name,
                    activeRepo.Path,
                    activeRepo.CurrentBranch,
                    activeRepo.IsClean,
                    activeRepo.ChangesCount
                } : null,
                repositories = repos.Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Path,
                    r.CurrentBranch,
                    r.IsClean,
                    r.ChangesCount
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting voice assistant context");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessCommand([FromBody] VoiceCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Command))
        {
            return BadRequest(new { success = false, spokenReply = "I didn't catch that. Please speak a command." });
        }

        var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        var repos = (await _scanner.ScanAsync(workingFolder)).ToList();

        var steps = new List<VoiceStep>();
        string commandText = request.Command.Trim();
        string normalizedCmd = Regex.Replace(commandText.ToLowerInvariant(), @"[^\w\s-]", " ");

        // Step 1: Detect repository from command or session
        Repository? targetRepo = ResolveTargetRepository(normalizedCmd, repos, request.TargetRepoPath);

        // Check if command is to list repositories
        if (IsListRepositoriesCommand(normalizedCmd))
        {
            steps.Add(new VoiceStep("Repository Discovery", "Listing discovered Git repositories in current workspace.", "completed"));
            
            var repoNames = repos.Select(r => r.Name).ToList();
            string spokenList = repoNames.Count > 0
                ? $"I found {repoNames.Count} repositories: {string.Join(", ", repoNames)}. Which one would you like to work with?"
                : "No repositories were found in the current workspace folder.";

            return Ok(new VoiceCommandResponse
            {
                Success = true,
                Intent = "list_repositories",
                Repository = targetRepo != null ? MapRepo(targetRepo) : null,
                Steps = steps,
                SpokenReply = spokenList,
                ActionTaken = "list_repositories",
                RawOutput = string.Join("\n", repos.Select(r => $"{r.Name} -> {r.Path} [{r.CurrentBranch}] (Changes: {r.ChangesCount})"))
            });
        }

        // Check if command is strictly a repository switch/selection
        if (IsSwitchRepositoryCommand(normalizedCmd) || (targetRepo != null && IsStrictRepoSelection(normalizedCmd, targetRepo.Name)))
        {
            if (targetRepo == null)
            {
                return Ok(new VoiceCommandResponse
                {
                    Success = false,
                    Intent = "select_repository",
                    Steps = new List<VoiceStep> { new("Repository Lookup", "Could not locate the requested repository.", "failed") },
                    SpokenReply = "I couldn't find a repository matching that name. Available repositories are: " + string.Join(", ", repos.Select(r => r.Name)),
                    ActionTaken = "none"
                });
            }

            HttpContext.Session.SetString("ActiveRepoPath", targetRepo.Path);

            steps.Add(new VoiceStep("Repository Selected", $"Switched active workspace context to '{targetRepo.Name}' at {targetRepo.Path}", "completed"));
            
            // Get fresh status
            steps.Add(new VoiceStep("Inspecting Status", $"Reading git status for {targetRepo.Name}...", "completed"));
            var status = await _gitService.ParseStatusAsync(targetRepo.Path);

            string cleanStatus = status.IsClean
                ? "The repository is clean with no uncommitted changes."
                : $"There are {status.Changes.Count} uncommitted changes.";

            string spoken = $"Selected repository {targetRepo.Name}. Active branch is {status.CurrentBranch ?? targetRepo.CurrentBranch ?? "unknown"}. {cleanStatus} What would you like to do?";

            return Ok(new VoiceCommandResponse
            {
                Success = true,
                Intent = "select_repository",
                Repository = MapRepo(targetRepo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "select_repository",
                RawOutput = $"Active: {targetRepo.Name}\nPath: {targetRepo.Path}\nBranch: {status.CurrentBranch}\nClean: {status.IsClean}"
            });
        }

        // If target repo still not resolved, try using the session's active repo
        if (targetRepo == null)
        {
            var sessionPath = HttpContext.Session.GetString("ActiveRepoPath");
            if (!string.IsNullOrWhiteSpace(sessionPath))
            {
                targetRepo = repos.FirstOrDefault(r => string.Equals(r.Path, sessionPath, StringComparison.OrdinalIgnoreCase));
            }

            if (targetRepo == null && repos.Count == 1)
            {
                targetRepo = repos[0];
            }
        }

        if (targetRepo == null)
        {
            return Ok(new VoiceCommandResponse
            {
                Success = false,
                Intent = "need_repo",
                Steps = new List<VoiceStep> { new("Context Check", "No active repository selected.", "failed") },
                SpokenReply = "Please specify which repository to work with first. For example, say: 'Select repository " + (repos.FirstOrDefault()?.Name ?? "MyProject") + "'.",
                ActionTaken = "prompt_repo_selection"
            });
        }

        // Ensure active repo is remembered
        HttpContext.Session.SetString("ActiveRepoPath", targetRepo.Path);

        // Execute specific Git / LGMS commands under target repository
        return await ExecuteRepoCommandAsync(targetRepo, normalizedCmd, commandText, steps);
    }

    private async Task<IActionResult> ExecuteRepoCommandAsync(
        Repository repo,
        string normalizedCmd,
        string rawCommand,
        List<VoiceStep> steps)
    {
        steps.Add(new VoiceStep("Context Active", $"Target repository: {repo.Name} ({repo.Path})", "completed"));

        // 1. GIT STATUS / CHECK CHANGES
        if (normalizedCmd.Contains("status") || normalizedCmd.Contains("what changed") || normalizedCmd.Contains("show changes") || normalizedCmd.Contains("dirty"))
        {
            steps.Add(new VoiceStep("Executing Git Status", $"Running git status --porcelain on {repo.Name}...", "running"));
            var status = await _gitService.ParseStatusAsync(repo.Path);
            steps[^1].Status = "completed";

            string statusVoice;
            if (status.IsClean)
            {
                statusVoice = $"Repository {repo.Name} on branch {status.CurrentBranch} is clean with zero uncommitted changes.";
            }
            else
            {
                var staged = status.StagedChanges.Count;
                var unstaged = status.UnstagedChanges.Count;
                var untracked = status.UntrackedFiles.Count;
                statusVoice = $"Repository {repo.Name} on branch {status.CurrentBranch} has {status.Changes.Count} total changes. {staged} staged, {unstaged} unstaged, and {untracked} untracked files.";
            }

            return Ok(new VoiceCommandResponse
            {
                Success = true,
                Intent = "status",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = statusVoice,
                ActionTaken = "status",
                RawOutput = string.Join("\n", status.Changes.Select(c => $"{(c.IsStaged ? "[STAGED]" : "[UNSTAGED]")} {c.Status}: {c.Path}"))
            });
        }

        // 2. GIT PULL
        if (normalizedCmd.Contains("pull") || normalizedCmd.Contains("sync") || normalizedCmd.Contains("update from remote"))
        {
            steps.Add(new VoiceStep("Git Pull", $"Pulling changes from remote origin into {repo.Name}...", "running"));
            var result = await _gitService.PullAsync(repo.Path);
            steps[^1].Status = result.Success ? "completed" : "failed";
            steps[^1].Output = result.Output ?? result.Error;

            LogOperation(repo.Name, "Voice Pull", result);

            string spoken = result.Success
                ? (result.Output?.Contains("Already up to date", StringComparison.OrdinalIgnoreCase) == true
                    ? $"Pulled latest changes for {repo.Name}. Everything is already up to date."
                    : $"Successfully pulled the latest changes into {repo.Name}.")
                : $"Failed to pull changes for {repo.Name}: {result.Error}";

            return Ok(new VoiceCommandResponse
            {
                Success = result.Success,
                Intent = "pull",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "pull",
                RawOutput = result.Output ?? result.Error
            });
        }

        // 3. GIT FETCH
        if (normalizedCmd.Contains("fetch"))
        {
            steps.Add(new VoiceStep("Git Fetch", $"Fetching from remote origin for {repo.Name}...", "running"));
            var result = await _gitService.FetchAsync(repo.Path);
            steps[^1].Status = result.Success ? "completed" : "failed";

            LogOperation(repo.Name, "Voice Fetch", result);

            string spoken = result.Success
                ? $"Successfully fetched all remotes for {repo.Name}."
                : $"Fetch failed for {repo.Name}: {result.Error}";

            return Ok(new VoiceCommandResponse
            {
                Success = result.Success,
                Intent = "fetch",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "fetch",
                RawOutput = result.Output ?? result.Error
            });
        }

        // 4. GIT PUSH
        if (normalizedCmd.Contains("push"))
        {
            steps.Add(new VoiceStep("Git Push", $"Pushing current commits to remote origin for {repo.Name}...", "running"));
            var result = await _gitService.PushAsync(repo.Path);
            steps[^1].Status = result.Success ? "completed" : "failed";

            LogOperation(repo.Name, "Voice Push", result);

            string spoken = result.Success
                ? $"Pushed all commits to remote successfully for {repo.Name}."
                : $"Push failed for {repo.Name}: {result.Error}";

            return Ok(new VoiceCommandResponse
            {
                Success = result.Success,
                Intent = "push",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "push",
                RawOutput = result.Output ?? result.Error
            });
        }

        // 5. CREATE BRANCH
        var createBranchMatch = Regex.Match(normalizedCmd, @"(?:create|make|new)\s+branch\s+([a-zA-Z0-9_\-\.\/]+)");
        if (createBranchMatch.Success)
        {
            string newBranch = createBranchMatch.Groups[1].Value.Trim();
            steps.Add(new VoiceStep("Create Branch", $"Creating and checking out new branch '{newBranch}' in {repo.Name}...", "running"));
            
            var result = await _gitService.CreateBranchAsync(repo.Path, newBranch);
            steps[^1].Status = result.Success ? "completed" : "failed";

            LogOperation(repo.Name, $"Voice Create Branch ({newBranch})", result);

            string spoken = result.Success
                ? $"Created and switched to new branch {newBranch} in {repo.Name}."
                : $"Failed to create branch {newBranch}: {result.Error}";

            return Ok(new VoiceCommandResponse
            {
                Success = result.Success,
                Intent = "create_branch",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "create_branch",
                RawOutput = result.Output ?? result.Error
            });
        }

        // 6. CHECKOUT / SWITCH BRANCH
        var checkoutMatch = Regex.Match(normalizedCmd, @"(?:checkout|switch to|switch)\s+(?:branch\s+)?([a-zA-Z0-9_\-\.\/]+)");
        if (checkoutMatch.Success)
        {
            string branchName = checkoutMatch.Groups[1].Value.Trim();
            steps.Add(new VoiceStep("Switch Branch", $"Checking out branch '{branchName}' in {repo.Name}...", "running"));
            
            var result = await _gitService.CheckoutAsync(repo.Path, branchName);
            steps[^1].Status = result.Success ? "completed" : "failed";

            LogOperation(repo.Name, $"Voice Checkout ({branchName})", result);

            string spoken = result.Success
                ? $"Switched branch to {branchName} in {repo.Name}."
                : $"Failed to checkout {branchName}: {result.Error}";

            return Ok(new VoiceCommandResponse
            {
                Success = result.Success,
                Intent = "checkout_branch",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "checkout",
                RawOutput = result.Output ?? result.Error
            });
        }

        // 7. COMMIT CHANGES
        var commitMatch = Regex.Match(rawCommand, @"(?:commit|commit with message)\s+(?:message\s+)?[""']?(.+?)[""']?$", RegexOptions.IgnoreCase);
        if (commitMatch.Success && !normalizedCmd.Contains("ai commit") && !normalizedCmd.Contains("suggest commit"))
        {
            string commitMsg = commitMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(commitMsg) || commitMsg.Equals("changes", StringComparison.OrdinalIgnoreCase))
            {
                commitMsg = "Update via LGMS Voice Assistant";
            }

            steps.Add(new VoiceStep("Stage All Changes", $"Staging all modified and new files in {repo.Name}...", "running"));
            var stageResult = await _gitService.StageAsync(repo.Path, new[] { "." });
            steps[^1].Status = stageResult.Success ? "completed" : "failed";

            if (stageResult.Success)
            {
                steps.Add(new VoiceStep("Commit", $"Committing with message: \"{commitMsg}\"...", "running"));
                var commitResult = await _gitService.CommitAsync(repo.Path, commitMsg);
                steps[^1].Status = commitResult.Success ? "completed" : "failed";

                LogOperation(repo.Name, "Voice Commit", commitResult);

                string spoken = commitResult.Success
                    ? $"Staged and committed all changes with message: {commitMsg}."
                    : $"Commit failed: {commitResult.Error}";

                return Ok(new VoiceCommandResponse
                {
                    Success = commitResult.Success,
                    Intent = "commit",
                    Repository = MapRepo(repo),
                    Steps = steps,
                    SpokenReply = spoken,
                    ActionTaken = "commit",
                    RawOutput = commitResult.Output ?? commitResult.Error
                });
            }
        }

        // 8. AI GENERATE COMMIT MESSAGE
        if (normalizedCmd.Contains("ai commit") || normalizedCmd.Contains("generate commit") || normalizedCmd.Contains("suggest commit"))
        {
            steps.Add(new VoiceStep("AI Commit Generator", $"Analyzing repository diff with AI provider...", "running"));
            try
            {
                var aiResult = await _aiManager.GenerateCommitMessageAsync(repo.Path, includeUnstaged: true);
                steps[^1].Status = aiResult.Success ? "completed" : "failed";

                string spoken = aiResult.Success
                    ? $"Generated commit message: \"{aiResult.Title}\". Would you like me to commit with this message?"
                    : $"Could not generate AI commit message: {aiResult.Error}";

                return Ok(new VoiceCommandResponse
                {
                    Success = aiResult.Success,
                    Intent = "ai_commit_message",
                    Repository = MapRepo(repo),
                    Steps = steps,
                    SpokenReply = spoken,
                    ActionTaken = "ai_commit",
                    RawOutput = aiResult.FullMessage
                });
            }
            catch (Exception ex)
            {
                steps[^1].Status = "failed";
                steps[^1].Output = ex.Message;
                return Ok(new VoiceCommandResponse
                {
                    Success = false,
                    Intent = "ai_commit_message",
                    Steps = steps,
                    SpokenReply = "Failed to generate AI commit message. Please verify your AI API key in Settings.",
                    ActionTaken = "ai_commit"
                });
            }
        }

        // 9. RECENT COMMITS / LOG
        if (normalizedCmd.Contains("log") || normalizedCmd.Contains("commits") || normalizedCmd.Contains("history"))
        {
            steps.Add(new VoiceStep("Commit History", $"Fetching recent git commits for {repo.Name}...", "running"));
            var commitsResult = await _gitService.GetCommitsAsync(repo.Path, 5);
            steps[^1].Status = commitsResult.Success ? "completed" : "failed";

            string spoken = commitsResult.Success
                ? $"Loaded the 5 most recent commits for {repo.Name}."
                : $"Failed to retrieve commit history: {commitsResult.Error}";

            return Ok(new VoiceCommandResponse
            {
                Success = commitsResult.Success,
                Intent = "log",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = spoken,
                ActionTaken = "log",
                RawOutput = commitsResult.Output
            });
        }

        // 10. OPEN IN VS CODE
        if (normalizedCmd.Contains("code") || normalizedCmd.Contains("editor"))
        {
            steps.Add(new VoiceStep("Open in Editor", $"Launching Visual Studio Code for {repo.Path}...", "running"));
            await _processService.ExecuteAsync("code", new[] { repo.Path }, repo.Path);
            steps[^1].Status = "completed";

            return Ok(new VoiceCommandResponse
            {
                Success = true,
                Intent = "open_code",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = $"Opening repository {repo.Name} in Visual Studio Code.",
                ActionTaken = "open_code"
            });
        }

        // 11. OPEN IN EXPLORER
        if (normalizedCmd.Contains("explorer") || normalizedCmd.Contains("open folder"))
        {
            steps.Add(new VoiceStep("Open in Explorer", $"Opening directory in File Explorer...", "running"));
            if (OperatingSystem.IsWindows())
            {
                await _processService.ExecuteAsync("explorer.exe", new[] { repo.Path }, repo.Path);
            }
            steps[^1].Status = "completed";

            return Ok(new VoiceCommandResponse
            {
                Success = true,
                Intent = "open_explorer",
                Repository = MapRepo(repo),
                Steps = steps,
                SpokenReply = $"Opening folder for {repo.Name} in Windows Explorer.",
                ActionTaken = "open_explorer"
            });
        }

        // Unrecognized fallback
        steps.Add(new VoiceStep("Command Analysis", $"Command '{rawCommand}' was recognized under {repo.Name}, but no matching action was found.", "warning"));
        return Ok(new VoiceCommandResponse
        {
            Success = false,
            Intent = "unknown",
            Repository = MapRepo(repo),
            Steps = steps,
            SpokenReply = $"I understood you're targeting {repo.Name}, but didn't recognize the action. You can say 'Status', 'Pull', 'Fetch', 'Push', 'Switch to branch main', or 'List repositories'.",
            ActionTaken = "unknown"
        });
    }

    private void LogOperation(string repoName, string op, GitResult res)
    {
        try
        {
            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = $"{op} ({repoName})",
                Status = res.Success ? "Success" : "Failed",
                Message = res.Success ? res.Output ?? "Success" : res.Error ?? "Failed",
                CreatedAt = DateTime.UtcNow
            });
            _db.SaveChanges();
        }
        catch
        {
            // Suppress logging failures
        }
    }

    private static Repository? ResolveTargetRepository(string normalizedCmd, List<Repository> repos, string? explicitPath)
    {
        // 1. If explicit path passed and valid
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var match = repos.FirstOrDefault(r => string.Equals(r.Path, explicitPath, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 2. Scan if any repo name is mentioned in command
        // Order by longest name first to avoid partial prefix collisions
        foreach (var repo in repos.OrderByDescending(r => r.Name.Length))
        {
            string cleanRepoName = Regex.Replace(repo.Name.ToLowerInvariant(), @"[^\w\s-]", " ").Trim();
            
            // Check direct containment
            if (normalizedCmd.Contains(cleanRepoName))
            {
                return repo;
            }

            // Check without underscores/hyphens
            string squishedRepo = cleanRepoName.Replace("_", "").Replace("-", "").Replace(" ", "");
            string squishedCmd = normalizedCmd.Replace("_", "").Replace("-", "").Replace(" ", "");
            if (squishedCmd.Contains(squishedRepo) && squishedRepo.Length >= 3)
            {
                return repo;
            }
        }

        return null;
    }

    private static bool IsListRepositoriesCommand(string cmd)
    {
        return cmd.Contains("list repo") ||
               cmd.Contains("list all repo") ||
               cmd.Contains("show all repo") ||
               cmd.Contains("show repositories") ||
               cmd.Contains("what repositories") ||
               cmd.Contains("what repos") ||
               cmd.Equals("repositories", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSwitchRepositoryCommand(string cmd)
    {
        return cmd.StartsWith("select repo") ||
               cmd.StartsWith("switch repo") ||
               cmd.StartsWith("switch to repo") ||
               cmd.StartsWith("go to repo") ||
               cmd.StartsWith("choose repo") ||
               cmd.StartsWith("open repo");
    }

    private static bool IsStrictRepoSelection(string cmd, string repoName)
    {
        var cleaned = cmd.Replace("select", "")
                         .Replace("switch to", "")
                         .Replace("switch", "")
                         .Replace("go to", "")
                         .Replace("repository", "")
                         .Replace("repo", "")
                         .Trim();

        return cleaned.Equals(repoName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    private static object MapRepo(Repository r)
    {
        return new
        {
            r.Id,
            r.Name,
            r.Path,
            r.CurrentBranch,
            r.IsClean,
            r.ChangesCount
        };
    }
}

public class VoiceCommandRequest
{
    public string Command { get; set; } = string.Empty;
    public string? TargetRepoPath { get; set; }
}

public class VoiceStep
{
    public VoiceStep(string title, string description, string status, string? output = null)
    {
        Title = title;
        Description = description;
        Status = status;
        Output = output;
    }

    public string Title { get; set; }
    public string Description { get; set; }
    public string Status { get; set; } // "pending", "running", "completed", "failed", "warning"
    public string? Output { get; set; }
}

public class VoiceCommandResponse
{
    public bool Success { get; set; }
    public string Intent { get; set; } = string.Empty;
    public object? Repository { get; set; }
    public List<VoiceStep> Steps { get; set; } = new();
    public string SpokenReply { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string? RawOutput { get; set; }
}
