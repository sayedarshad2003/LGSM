using LGMS.Models;
using LGMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class TerminalController : Controller
{
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;

    public TerminalController(IGitService gitService, IRepositoryScanner scanner)
    {
        _gitService = gitService;
        _scanner = scanner;
    }

    [HttpGet("repository/{id}/terminal")]
    public async Task<IActionResult> Index(string id, [FromQuery] string? path)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        ViewBag.Repository = repo;
        return View();
    }

    [HttpPost("api/git/terminal/run")]
    public async Task<IActionResult> Run([FromBody] TerminalCommandRequest request)
    {
        var repoPath = !string.IsNullOrWhiteSpace(request?.Path) ? _scanner.NormalizePath(request.Path) : string.Empty;
        if (string.IsNullOrWhiteSpace(repoPath) || !Directory.Exists(repoPath))
        {
            return BadRequest(new { success = false, error = "Invalid repository path." });
        }

        if (string.IsNullOrWhiteSpace(request?.Command))
        {
            return BadRequest(new { success = false, error = "Command cannot be empty." });
        }

        var result = await _gitService.RunCommandAsync(repoPath, request.Command);
        return Json(result);
    }

    private async Task<Repository?> GetRepoAsync(string id, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            return await _scanner.GetRepositoryDetailsAsync(path);
        }
        var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        var repos = await _scanner.ScanAsync(workingFolder);
        return repos.FirstOrDefault(r => r.Id == id);
    }
}

public class TerminalCommandRequest
{
    public string Path { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}
