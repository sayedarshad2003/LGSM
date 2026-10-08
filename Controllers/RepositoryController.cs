using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class RepositoryController : Controller
{
    private readonly IRepositoryScanner _scanner;
    private readonly IGitService _gitService;
    private readonly IProcessService _processService;

    public RepositoryController(IRepositoryScanner scanner, IGitService gitService, IProcessService processService)
    {
        _scanner = scanner;
        _gitService = gitService;
        _processService = processService;
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
}

public class RepoActionRequest
{
    public string Path { get; set; } = string.Empty;
}
