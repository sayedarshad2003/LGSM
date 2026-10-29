using LGMS.Data;
using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGMS.Controllers;

public class RepositoryController : Controller
{
    private readonly IRepositoryScanner _scanner;
    private readonly IGitService _gitService;
    private readonly IProcessService _processService;
    private readonly LgmsDbContext _db;

    public RepositoryController(IRepositoryScanner scanner, IGitService gitService, IProcessService processService, LgmsDbContext db)
    {
        _scanner = scanner;
        _gitService = gitService;
        _processService = processService;
        _db = db;
    }

    [HttpGet("repositories")]
    public async Task<IActionResult> Index([FromQuery] string? search, [FromQuery] string? sort)
    {
        var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        var repos = await _scanner.ScanAsync(workingFolder);

        if (!string.IsNullOrWhiteSpace(search))
        {
            repos = repos.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                     r.Path.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        repos = sort switch
        {
            "changes" => repos.OrderByDescending(r => r.ChangesCount).ToList(),
            "branch" => repos.OrderBy(r => r.CurrentBranch).ToList(),
            _ => repos.OrderBy(r => r.Name).ToList()
        };

        var viewModel = new RepositoryViewModel
        {
            Repositories = repos,
            WorkingFolder = workingFolder,
            SearchQuery = search ?? string.Empty,
            SortBy = sort ?? "name"
        };

        return View(viewModel);
    }

    [HttpGet("api/repositories")]
    public async Task<IActionResult> GetRepositoriesApi([FromQuery] string? folder)
    {
        var targetFolder = folder ?? HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        if (!Directory.Exists(targetFolder))
        {
            return BadRequest(new { success = false, message = "Directory not found." });
        }

        var repos = await _scanner.ScanAsync(targetFolder);
        return Json(new { success = true, repositories = repos, total = repos.Count });
    }

    [HttpGet("repository/{id}")]
    public async Task<IActionResult> Dashboard(string id, [FromQuery] string? path)
    {
        var repoPath = path;
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
            var repos = await _scanner.ScanAsync(workingFolder);
            var found = repos.FirstOrDefault(r => r.Id == id);
            if (found == null) return NotFound("Repository not found in current workspace.");
            repoPath = found.Path;
        }

        var repoDetails = await _scanner.GetRepositoryDetailsAsync(repoPath);
        if (repoDetails == null) return NotFound("Repository details could not be retrieved.");

        var status = await _gitService.ParseStatusAsync(repoPath);
        var viewModel = new DashboardViewModel
        {
            Repository = repoDetails,
            Status = status,
            IsVsCodeInstalled = await _processService.IsCommandAvailableAsync("code")
        };

        return View(viewModel);
    }

    [HttpPost("repository/open-folder")]
    public async Task<IActionResult> OpenFolder([FromBody] RepoActionRequest request)
    {
        if (OperatingSystem.IsWindows())
        {
            await _processService.ExecuteAsync("explorer.exe", new[] { request.Path }, Environment.CurrentDirectory);
        }
        else if (OperatingSystem.IsMacOS())
        {
            await _processService.ExecuteAsync("open", new[] { request.Path }, Environment.CurrentDirectory);
        }
        else
        {
            await _processService.ExecuteAsync("xdg-open", new[] { request.Path }, Environment.CurrentDirectory);
        }
        return Json(new { success = true });
    }

    [HttpPost("repository/open-vscode")]
    public async Task<IActionResult> OpenVsCode([FromBody] RepoActionRequest request)
    {
        var result = await _processService.ExecuteAsync("code", new[] { request.Path }, request.Path);
        return Json(new { success = result.Success, output = result.StandardOutput, error = result.StandardError });
    }

    [HttpPost("api/repository/clone")]
    public async Task<IActionResult> CloneRepository([FromBody] CloneRepoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.RepositoryUrl))
        {
            return BadRequest(new { success = false, message = "Remote repository URL cannot be empty." });
        }

        if (string.IsNullOrWhiteSpace(request?.DestinationPath))
        {
            return BadRequest(new { success = false, message = "Destination folder cannot be empty. Please select a folder first." });
        }

        try
        {
            var destination = _scanner.NormalizePath(request.DestinationPath.Trim());
            var result = await _gitService.CloneAsync(request.RepositoryUrl.Trim(), destination, ct);

            var finalPath = !string.IsNullOrWhiteSpace(result.Output) && Directory.Exists(result.Output)
                ? _scanner.NormalizePath(result.Output)
                : destination;

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = "Clone Repository",
                Status = result.Success ? "Success" : "Failed",
                Message = result.Success
                    ? $"Cloned {request.RepositoryUrl} into {finalPath}"
                    : result.Error,
                CreatedAt = DateTime.UtcNow
            });

            if (result.Success)
            {
                var existing = await _db.LocalRepositories
                    .FirstOrDefaultAsync(r => r.LocalPath == finalPath, ct);

                if (existing == null)
                {
                    _db.LocalRepositories.Add(new LocalRepository
                    {
                        Name = Path.GetFileName(finalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                        LocalPath = finalPath,
                        LastScannedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync(ct);

                var details = await _scanner.GetRepositoryDetailsAsync(finalPath);
                return Json(new
                {
                    success = true,
                    message = $"Repository cloned successfully into {finalPath}!",
                    repositoryPath = finalPath,
                    repositoryId = details?.Id
                });
            }
            else
            {
                await _db.SaveChangesAsync(ct);
                return Json(new { success = false, message = result.Error ?? "Clone operation failed." });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Clone error: {ex.Message}" });
        }
    }

    [HttpPost("api/repository/init")]
    public async Task<IActionResult> InitProject([FromBody] InitProjectRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Path))
        {
            return BadRequest(new { success = false, message = "Please select or enter a project folder to initialize." });
        }

        try
        {
            var folder = _scanner.NormalizePath(request.Path.Trim());
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var commitMsg = string.IsNullOrWhiteSpace(request.CommitMessage) ? "Initial Commit" : request.CommitMessage.Trim();
            var result = await _gitService.InitAndInitialCommitAsync(folder, commitMsg);

            _db.GitOperationLogs.Add(new GitOperationLog
            {
                Operation = "Add Project to Git",
                Status = result.Success ? "Success" : "Failed",
                Message = result.Success
                    ? $"Initialized Git repository and created '{commitMsg}' in {folder}"
                    : result.Error,
                CreatedAt = DateTime.UtcNow
            });

            if (result.Success)
            {
                var existing = await _db.LocalRepositories
                    .FirstOrDefaultAsync(r => r.LocalPath == folder, ct);

                if (existing == null)
                {
                    _db.LocalRepositories.Add(new LocalRepository
                    {
                        Name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                        LocalPath = folder,
                        LastScannedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync(ct);

                var details = await _scanner.GetRepositoryDetailsAsync(folder);
                return Json(new
                {
                    success = true,
                    message = $"Project initialized and committed as '{commitMsg}' successfully!",
                    repositoryPath = folder,
                    repositoryId = details?.Id
                });
            }
            else
            {
                await _db.SaveChangesAsync(ct);
                return Json(new { success = false, message = result.Error ?? "Git initialization failed." });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Init error: {ex.Message}" });
        }
    }
}

public class RepoActionRequest
{
    public string Path { get; set; } = string.Empty;
}

public class CloneRepoRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}

public class InitProjectRequest
{
    public string Path { get; set; } = string.Empty;
    public string? CommitMessage { get; set; }
}
