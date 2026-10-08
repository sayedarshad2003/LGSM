using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class BranchController : Controller
{
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;

    public BranchController(IGitService gitService, IRepositoryScanner scanner)
    {
        _gitService = gitService;
        _scanner = scanner;
    }

    [HttpGet("repository/{id}/branches")]
    public async Task<IActionResult> Index(string id, [FromQuery] string? path)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        var branchesResult = await _gitService.GetBranchesAsync(repo.Path);
        var viewModel = new BranchViewModel
        {
            Repository = repo,
            CurrentBranch = repo.CurrentBranch
        };

        if (branchesResult.Success && !string.IsNullOrWhiteSpace(branchesResult.Output))
        {
            var lines = branchesResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                var name = parts[0].Trim();
                var isHead = parts.Length > 1 && parts[1].Trim() == "*";
                var upstream = parts.Length > 2 ? parts[2].Trim() : null;
                var commitHash = parts.Length > 3 ? parts[3].Trim() : null;
                var subject = parts.Length > 4 ? parts[4].Trim() : null;

                var isRemote = name.StartsWith("remotes/") || name.StartsWith("origin/");
                var branch = new GitBranch
                {
                    Name = name.Replace("remotes/", ""),
                    IsCurrent = isHead,
                    IsRemote = isRemote,
                    Upstream = string.IsNullOrWhiteSpace(upstream) ? null : upstream,
                    LatestCommitHash = commitHash,
                    LatestCommitSubject = subject
                };

                if (isRemote)
                {
                    viewModel.RemoteBranches.Add(branch);
                }
                else
                {
                    viewModel.LocalBranches.Add(branch);
                }
            }
        }

        return View(viewModel);
    }

    [HttpPost("api/git/branch/checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutBranchRequest request)
    {
        var result = await _gitService.CheckoutAsync(request.Path, request.Branch);
        return Json(result);
    }

    [HttpPost("api/git/branch/create")]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
        {
            return BadRequest(new { success = false, error = "Branch name cannot be empty." });
        }
        var result = await _gitService.CreateBranchAsync(request.Path, request.Branch.Trim());
        return Json(result);
    }

    [HttpPost("api/git/branch/delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Branch))
        {
            return BadRequest(new { success = false, error = "Branch name required." });
        }
        var result = await _gitService.DeleteBranchAsync(request.Path, request.Branch, request.Force);
        return Json(result);
    }

    [HttpPost("api/git/branch/merge")]
    public async Task<IActionResult> Merge([FromBody] MergeBranchRequest request)
    {
        var result = await _gitService.MergeBranchAsync(request.Path, request.Branch);
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

public class CheckoutBranchRequest
{
    public string Path { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}

public class CreateBranchRequest
{
    public string Path { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}

public class DeleteBranchRequest
{
    public string Path { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public bool Force { get; set; }
}

public class MergeBranchRequest
{
    public string Path { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}
