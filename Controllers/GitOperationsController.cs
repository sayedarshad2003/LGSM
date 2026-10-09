using LGMS.Data;
using LGMS.Models;
using LGMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public sealed class GitOperationsController : Controller
{
    private readonly IGitService _git;
    private readonly GitHubCliService _github;
    private readonly LgmsDbContext _db;
    private readonly ILogger<GitOperationsController> _logger;

    public GitOperationsController(
        IGitService git,
        GitHubCliService github,
        LgmsDbContext db,
        ILogger<GitOperationsController> logger)
    {
        _git = git;
        _github = github;
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fetch(
        string path,
        string remote,
        string? host,
        string? username,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            TempData["ErrorMessage"] = "Invalid repository path.";
            return RedirectToAction("Index", "Home");
        }

        remote = string.IsNullOrWhiteSpace(remote) ? "origin" : remote;

        try
        {
            GitResult result;
            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username))
            {
                result = await _github.ExecuteForAccountAsync(
                    host,
                    username,
                    _ => _git.FetchAsync(path, remote),
                    ct);
            }
            else
            {
                result = await _git.FetchAsync(path, remote);
            }

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = $"Fetch ({remote})",
                Status = result.Success ? "Success" : "Failed",
                Message = result.Success ? $"Fetched remote '{remote}'" : result.Error,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Fetch from '{remote}' completed successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Fetch failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Fetch on {Path}", path);
            TempData["ErrorMessage"] = $"Fetch exception: {ex.Message}";
        }

        return RedirectToAction("Index", "Git", new { path });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pull(
        string path,
        string remote,
        string branch,
        string? host,
        string? username,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            TempData["ErrorMessage"] = "Invalid repository path.";
            return RedirectToAction("Index", "Home");
        }

        remote = string.IsNullOrWhiteSpace(remote) ? "origin" : remote;
        branch = string.IsNullOrWhiteSpace(branch) ? "HEAD" : branch;

        try
        {
            GitResult result;
            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username))
            {
                result = await _github.ExecuteForAccountAsync(
                    host,
                    username,
                    _ => _git.PullAsync(path, remote, branch),
                    ct);
            }
            else
            {
                result = await _git.PullAsync(path, remote, branch);
            }

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = $"Pull ({remote}/{branch})",
                Status = result.Success ? "Success" : "Failed",
                Message = result.Success ? $"Pulled from {remote}/{branch}" : result.Error,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Pull from '{remote}/{branch}' completed successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Pull failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Pull on {Path}", path);
            TempData["ErrorMessage"] = $"Pull exception: {ex.Message}";
        }

        return RedirectToAction("Index", "Git", new { path });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Push(
        string path,
        string remote,
        string branch,
        string? host,
        string? username,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            TempData["ErrorMessage"] = "Invalid repository path.";
            return RedirectToAction("Index", "Home");
        }

        remote = string.IsNullOrWhiteSpace(remote) ? "origin" : remote;
        branch = string.IsNullOrWhiteSpace(branch) ? "HEAD" : branch;

        try
        {
            GitResult result;
            if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(username))
            {
                result = await _github.ExecuteForAccountAsync(
                    host,
                    username,
                    _ => _git.PushAsync(path, remote, branch),
                    ct);
            }
            else
            {
                result = await _git.PushAsync(path, remote, branch);
            }

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = $"Push ({remote}/{branch})",
                Status = result.Success ? "Success" : "Failed",
                Message = result.Success ? $"Pushed to {remote}/{branch}" : result.Error,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Push to '{remote}/{branch}' succeeded!";
            }
            else
            {
                TempData["ErrorMessage"] = $"Push failed: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Push on {Path}", path);
            TempData["ErrorMessage"] = $"Push exception: {ex.Message}";
        }

        return RedirectToAction("Index", "Git", new { path });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRemote(
        string path,
        string name,
        string url,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            TempData["ErrorMessage"] = "Remote name and URL are required.";
            return RedirectToAction("Index", "Git", new { path });
        }

        try
        {
            var result = await _git.AddRemoteAsync(path, name, url);
            if (result.Success)
            {
                _db.GitOperationLogs.Add(new GitOperationLog
                {
                    Operation = "Add Remote",
                    Status = "Success",
                    Message = $"Added remote '{name}' with URL '{url}'",
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = $"Remote '{name}' added successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to add remote: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Add remote error: {ex.Message}";
        }

        return RedirectToAction("Remotes", "Git", new { path });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRemote(
        string path,
        string name,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Remote name is required.";
            return RedirectToAction("Index", "Git", new { path });
        }

        try
        {
            var result = await _git.RemoveRemoteAsync(path, name);
            if (result.Success)
            {
                _db.GitOperationLogs.Add(new GitOperationLog
                {
                    Operation = "Remove Remote",
                    Status = "Success",
                    Message = $"Removed remote '{name}'",
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
                TempData["SuccessMessage"] = $"Remote '{name}' removed.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to remove remote: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Remove remote error: {ex.Message}";
        }

        return RedirectToAction("Remotes", "Git", new { path });
    }
}
