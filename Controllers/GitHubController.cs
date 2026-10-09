using LGMS.Data;
using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGMS.Controllers;

public sealed class GitHubController : Controller
{
    private readonly GitHubCliService _github;
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;
    private readonly LgmsDbContext _db;
    private readonly ILogger<GitHubController> _logger;

    public GitHubController(
        GitHubCliService github,
        IGitService gitService,
        IRepositoryScanner scanner,
        LgmsDbContext db,
        ILogger<GitHubController> logger)
    {
        _github = github;
        _gitService = gitService;
        _scanner = scanner;
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new GitHubAccountsViewModel();

        try
        {
            var accounts = await _github.GetAccountsAsync(ct);
            model.Accounts = accounts;

            // Sync to MS SQL Database
            foreach (var acc in accounts)
            {
                var existing = await _db.GitHubAccounts
                    .SingleOrDefaultAsync(x => x.Host == acc.Host && x.Username == acc.Username, ct);

                if (existing == null)
                {
                    _db.GitHubAccounts.Add(new GitHubAccount
                    {
                        Host = acc.Host,
                        Username = acc.Username,
                        IsActive = acc.IsActive,
                        LastVerifiedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.IsActive = acc.IsActive;
                    existing.LastVerifiedAt = DateTime.UtcNow;
                }
            }

            // Remove any accounts no longer in CLI
            var dbAccounts = await _db.GitHubAccounts.ToListAsync(ct);
            foreach (var dbAcc in dbAccounts)
            {
                if (!accounts.Any(a => a.Host == dbAcc.Host && a.Username == dbAcc.Username))
                {
                    _db.GitHubAccounts.Remove(dbAcc);
                }
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve GitHub CLI accounts.");
            model.ErrorMessage = $"GitHub CLI Error: {ex.Message}";
        }

        try
        {
            model.RecentLogs = await _db.GitOperationLogs
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load recent logs from database.");
        }

        if (TempData["SuccessMessage"] != null)
        {
            model.SuccessMessage = TempData["SuccessMessage"]?.ToString();
        }
        if (TempData["ErrorMessage"] != null)
        {
            model.ErrorMessage = TempData["ErrorMessage"]?.ToString();
        }

        return View(model);
    }

    public async Task<IActionResult> Repositories(string? host, string? username, string? search, CancellationToken ct)
    {
        var model = new GitHubRepositoriesViewModel
        {
            SearchQuery = search ?? string.Empty
        };

        try
        {
            var accounts = await _github.GetAccountsAsync(ct);
            model.AllAccounts = accounts;

            if (accounts.Count == 0)
            {
                TempData["ErrorMessage"] = "No authenticated GitHub accounts found. Please authenticate an account first.";
                return RedirectToAction(nameof(Index));
            }

            var selectedAccount = accounts.FirstOrDefault(a =>
                (string.IsNullOrWhiteSpace(host) || a.Host.Equals(host, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(username) || a.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                ?? accounts.FirstOrDefault(a => a.IsActive)
                ?? accounts[0];

            model.Host = selectedAccount.Host;
            model.Username = selectedAccount.Username;

            var repos = await _github.ListRepositoriesAsync(model.Host, model.Username, ct);
            if (!string.IsNullOrWhiteSpace(search))
            {
                repos = repos.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                         r.Owner.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            model.Repositories = repos;

            // Load local repos from scanner and database to check existing clones
            var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
            var scanned = await _scanner.ScanAsync(workingFolder, ct);
            var dbRepos = await _db.LocalRepositories.ToListAsync(ct);
            var knownPaths = scanned.Select(r => r.Path.ToLowerInvariant())
                .Concat(dbRepos.Select(r => r.LocalPath.ToLowerInvariant()))
                .ToHashSet();

            model.ExistingLocalPaths = knownPaths;
            model.DefaultCloneRoot = workingFolder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list GitHub repositories for user {Username}", username);
            model.ErrorMessage = ex.Message;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchAccount(string host, string username, string? returnUrl, CancellationToken ct)
    {
        try
        {
            await _github.SwitchAccountAsync(host, username, ct);

            // Update database active flags
            var accounts = await _db.GitHubAccounts.ToListAsync(ct);
            foreach (var acc in accounts)
            {
                acc.IsActive = (acc.Host.Equals(host, StringComparison.OrdinalIgnoreCase) &&
                                acc.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = "Switch GitHub Account",
                Status = "Success",
                Message = $"Switched active account to @{username} on {host}",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = $"Active GitHub account switched to @{username}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching account to {Username}", username);
            TempData["ErrorMessage"] = $"Failed to switch account: {ex.Message}";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clone(CloneRepositoryViewModel model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.RepositoryUrl) || string.IsNullOrWhiteSpace(model.DestinationPath))
        {
            TempData["ErrorMessage"] = "Repository URL and Destination Path are required.";
            return RedirectToAction(nameof(Repositories), new { host = model.Host, username = model.Username });
        }

        try
        {
            var result = await _github.ExecuteForAccountAsync(
                model.Host,
                model.Username,
                token => _gitService.CloneAsync(model.RepositoryUrl, model.DestinationPath, token),
                ct);

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = "Clone Repository",
                Status = result.Success ? "Success" : "Failed",
                Message = result.Success
                    ? $"Cloned {model.RepositoryName} into {model.DestinationPath}"
                    : result.Error,
                CreatedAt = DateTime.UtcNow
            });

            if (result.Success)
            {
                var existing = await _db.LocalRepositories
                    .FirstOrDefaultAsync(r => r.LocalPath == model.DestinationPath, ct);

                if (existing == null)
                {
                    _db.LocalRepositories.Add(new LocalRepository
                    {
                        Name = !string.IsNullOrWhiteSpace(model.RepositoryName) ? model.RepositoryName : Path.GetFileName(model.DestinationPath),
                        LocalPath = model.DestinationPath,
                        LastScannedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync(ct);

                TempData["SuccessMessage"] = $"Successfully cloned {model.RepositoryName} to {model.DestinationPath}!";
                return RedirectToAction("Index", "Repository");
            }
            else
            {
                await _db.SaveChangesAsync(ct);
                TempData["ErrorMessage"] = $"Clone failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while cloning repository {Url}", model.RepositoryUrl);
            TempData["ErrorMessage"] = $"Clone error: {ex.Message}";
        }

        return RedirectToAction(nameof(Repositories), new { host = model.Host, username = model.Username });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupGit(CancellationToken ct)
    {
        try
        {
            var res = await _github.SetupGitCredentialsAsync(ct);
            if (res.Success)
            {
                TempData["SuccessMessage"] = "Configured Git to authenticate through GitHub CLI (gh auth setup-git).";
            }
            else
            {
                TempData["ErrorMessage"] = $"Setup Git failed: {res.Error}";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error setting up Git credentials: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
