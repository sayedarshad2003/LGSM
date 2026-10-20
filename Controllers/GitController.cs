using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class GitController : Controller
{
    private readonly IGitService _gitService;
    private readonly IRepositoryScanner _scanner;
    private readonly IAICommitManager _aiManager;

    public GitController(
        IGitService gitService,
        IRepositoryScanner scanner,
        IAICommitManager aiManager)
    {
        _gitService = gitService;
        _scanner = scanner;
        _aiManager = aiManager;
    }

    [HttpGet("repository/{id}/changes")]
    public async Task<IActionResult> Changes(string id, [FromQuery] string? path)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        var status = await _gitService.ParseStatusAsync(repo.Path);
        var viewModel = new ChangesViewModel
        {
            Repository = repo,
            Status = status
        };

        return View(viewModel);
    }

    [HttpGet("repository/{id}/diff")]
    public async Task<IActionResult> Diff(string id, [FromQuery] string? path, [FromQuery] string? file, [FromQuery] bool staged = false)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        var diffResult = await _gitService.DiffAsync(repo.Path, staged, file);
        var status = await _gitService.ParseStatusAsync(repo.Path);

        var viewModel = new ChangesViewModel
        {
            Repository = repo,
            Status = status,
            SelectedFilePath = file,
            DiffContent = diffResult.Output,
            IsDiffStaged = staged
        };

        return View(viewModel);
    }

    [HttpGet("repository/{id}/stashes")]
    public async Task<IActionResult> Stashes(string id, [FromQuery] string? path)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        var result = await _gitService.GetStashesAsync(repo.Path);
        var stashes = new List<GitStash>();

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int idx = 0;
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                stashes.Add(new GitStash
                {
                    Index = idx++,
                    Message = parts.Length > 1 ? parts[1] : line,
                    Date = parts.Length > 2 ? parts[2] : string.Empty
                });
            }
        }

        ViewBag.Repository = repo;
        return View(stashes);
    }

    [HttpGet("repository/{id}/remotes")]
    public async Task<IActionResult> Remotes(string id, [FromQuery] string? path)
    {
        var repo = await GetRepoAsync(id, path);
        if (repo == null) return NotFound();

        var result = await _gitService.GetRemotesAsync(repo.Path);
        var remotes = new List<GitRemote>();

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var map = new Dictionary<string, GitRemote>();
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var name = parts[0];
                    var url = parts[1];
                    var isPush = line.Contains("(push)");
                    if (!map.TryGetValue(name, out var remote))
                    {
                        remote = new GitRemote { Name = name, FetchUrl = url, PushUrl = url };
                        map[name] = remote;
                    }
                    if (isPush) remote.PushUrl = url;
                    else remote.FetchUrl = url;
                }
            }
            remotes = map.Values.ToList();
        }

        ViewBag.Repository = repo;
        return View(remotes);
    }

    // --- JSON API Endpoints for AJAX operations ---

    [HttpPost("api/git/status")]
    public async Task<IActionResult> GetStatusJson([FromBody] GitPathRequest request)
    {
        var status = await _gitService.ParseStatusAsync(request.Path);
        return Json(new { success = true, status });
    }

    [HttpPost("api/git/stage")]
    public async Task<IActionResult> Stage([FromBody] GitFilesRequest request)
    {
        var result = await _gitService.StageAsync(request.Path, request.Files);
        return Json(result);
    }

    [HttpPost("api/git/unstage")]
    public async Task<IActionResult> Unstage([FromBody] GitFilesRequest request)
    {
        var result = await _gitService.UnstageAsync(request.Path, request.Files);
        return Json(result);
    }

    [HttpPost("api/git/discard")]
    public async Task<IActionResult> Discard([FromBody] GitFilesRequest request)
    {
        var result = await _gitService.DiscardAsync(request.Path, request.Files);
        return Json(result);
    }

    [HttpPost("api/git/commit")]
    public async Task<IActionResult> Commit([FromBody] GitCommitRequest request)
    {
        var result = await _gitService.CommitAsync(request.Path, request.Message);
        return Json(result);
    }

    [HttpPost("api/git/generate-commit-message")]
    public async Task<IActionResult> GenerateCommitMessage([FromBody] GitAutoCommitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { success = false, error = "Path is required." });
        }

        var sessionProvider = HttpContext.Session.GetString("ActiveAiProvider");
        var sessionModel = HttpContext.Session.GetString("SelectedAiModel");

        var provider = !string.IsNullOrWhiteSpace(request.Provider) ? request.Provider : sessionProvider;
        var model = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : sessionModel;

        var result = await _aiManager.GenerateCommitMessageAsync(
            request.Path,
            provider,
            model,
            request.IncludeUnstaged,
            request.Prompt,
            ct);

        return Json(result);
    }

    [HttpGet("api/git/ai-models")]
    public IActionResult GetAiModels()
    {
        var models = _aiManager.GetAvailableModels();
        var defaultProvider = HttpContext.Session.GetString("ActiveAiProvider");
        var defaultModel = HttpContext.Session.GetString("SelectedAiModel");
        return Json(new { success = true, models, defaultProvider, defaultModel });
    }

    [HttpPost("api/git/push")]
    public async Task<IActionResult> Push([FromBody] GitPathRequest request)
    {
        var result = await _gitService.PushAsync(request.Path);
        return Json(result);
    }

    [HttpPost("api/git/pull")]
    public async Task<IActionResult> Pull([FromBody] GitPathRequest request)
    {
        var status = await _gitService.ParseStatusAsync(request.Path);
        if (!status.IsClean && !request.Force)
        {
            return Json(new GitResult
            {
                Success = false,
                Error = "Local uncommitted changes detected. Pull may cause conflicts. Please stage or stash your changes, or confirm to proceed."
            });
        }

        var result = await _gitService.PullAsync(request.Path);
        return Json(result);
    }

    [HttpPost("api/git/fetch")]
    public async Task<IActionResult> Fetch([FromBody] GitPathRequest request)
    {
        var result = await _gitService.FetchAsync(request.Path);
        return Json(result);
    }

    [HttpPost("api/git/diff")]
    public async Task<IActionResult> DiffApi([FromBody] GitDiffRequest request)
    {
        var result = await _gitService.DiffAsync(request.Path, request.Staged, request.File);
        return Json(result);
    }

    [HttpPost("api/git/stash/save")]
    public async Task<IActionResult> StashSave([FromBody] GitStashRequest request)
    {
        var result = await _gitService.StashSaveAsync(request.Path, request.Message);
        return Json(result);
    }

    [HttpPost("api/git/stash/apply")]
    public async Task<IActionResult> StashApply([FromBody] GitStashRefRequest request)
    {
        var result = await _gitService.StashApplyAsync(request.Path, request.StashRef);
        return Json(result);
    }

    [HttpPost("api/git/stash/pop")]
    public async Task<IActionResult> StashPop([FromBody] GitStashRefRequest request)
    {
        var result = await _gitService.StashPopAsync(request.Path, request.StashRef);
        return Json(result);
    }

    [HttpPost("api/git/stash/drop")]
    public async Task<IActionResult> StashDrop([FromBody] GitStashRefRequest request)
    {
        var result = await _gitService.StashDropAsync(request.Path, request.StashRef);
        return Json(result);
    }

    [HttpPost("api/git/remote/add")]
    public async Task<IActionResult> RemoteAdd([FromBody] GitRemoteAddRequest request)
    {
        var result = await _gitService.AddRemoteAsync(request.Path, request.Name, request.Url);
        return Json(result);
    }

    [HttpPost("api/git/remote/remove")]
    public async Task<IActionResult> RemoteRemove([FromBody] GitRemoteRemoveRequest request)
    {
        var result = await _gitService.RemoveRemoteAsync(request.Path, request.Name);
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

public class GitPathRequest
{
    public string Path { get; set; } = string.Empty;
    public bool Force { get; set; }
}

public class GitFilesRequest
{
    public string Path { get; set; } = string.Empty;
    public List<string> Files { get; set; } = new();
}

public class GitCommitRequest
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class GitDiffRequest
{
    public string Path { get; set; } = string.Empty;
    public bool Staged { get; set; }
    public string? File { get; set; }
}

public class GitStashRequest
{
    public string Path { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class GitStashRefRequest
{
    public string Path { get; set; } = string.Empty;
    public string StashRef { get; set; } = string.Empty;
}

public class GitRemoteAddRequest
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class GitRemoteRemoveRequest
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class GitAutoCommitRequest
{
    public string Path { get; set; } = string.Empty;
    public bool IncludeUnstaged { get; set; } = true;
    public string? Prompt { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
}


