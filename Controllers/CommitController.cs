using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class CommitController : Controller
{
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;
    private readonly IProcessService _processService;

    public CommitController(IGitService gitService, IRepositoryScanner scanner, IProcessService processService)
    {
        _gitService = gitService;
        _scanner = scanner;
        _processService = processService;
    }

    [HttpGet("repository/{id}/commits")]
    public async Task<IActionResult> Index(string id, [FromQuery] string? path, [FromQuery] int count = 50)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        var commitsResult = await _gitService.GetCommitsAsync(repo.Path, count);
        var commits = new List<GitCommit>();

        if (commitsResult.Success && !string.IsNullOrWhiteSpace(commitsResult.Output))
        {
            var lines = commitsResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 7)
                {
                    DateTime.TryParse(parts[4], out var parsedDate);
                    commits.Add(new GitCommit
                    {
                        Hash = parts[0],
                        AuthorName = parts[2],
                        AuthorEmail = parts[3],
                        Date = parsedDate,
                        RelativeTime = parts[5],
                        Subject = parts[6],
                        Body = parts.Length > 7 ? parts[7] : string.Empty
                    });
                }
            }
        }

        ViewBag.Repository = repo;
        return View(commits);
    }

    [HttpGet("api/git/commit/details")]
    public async Task<IActionResult> Details([FromQuery] string path, [FromQuery] string hash)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash))
        {
            return BadRequest(new { success = false, error = "Path and Hash are required." });
        }

        // Show diff of commit
        var diffResult = await _processService.ExecuteAsync("git", new[] { "show", "--stat", "--patch", hash }, path);
        // Show files changed
        var filesResult = await _processService.ExecuteAsync("git", new[] { "show", "--name-only", "--oneline", hash }, path);

        var fileLines = filesResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var changedFiles = fileLines.Skip(1).ToList();

        return Json(new
        {
            success = diffResult.Success,
            hash,
            diff = diffResult.StandardOutput,
            changedFiles,
            filesCount = changedFiles.Count
        });
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
