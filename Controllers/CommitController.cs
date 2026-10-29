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

        // Full diff of commit
        var diffResult = await _processService.ExecuteAsync("git", new[] { "show", "--stat", "--patch", hash }, path);
        
        // Structured file list with status and line additions/deletions
        var changedFiles = await _gitService.GetCommitFilesAsync(path, hash);

        return Json(new
        {
            success = diffResult.Success,
            hash,
            diff = diffResult.StandardOutput,
            changedFiles,
            filesCount = changedFiles.Count
        });
    }

    [HttpGet("api/git/commit/file-diff")]
    public async Task<IActionResult> FileDiff([FromQuery] string path, [FromQuery] string hash, [FromQuery] string file)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(new { success = false, error = "Path, Hash, and File are required." });
        }

        var result = await _gitService.GetCommitFileDiffAsync(path, hash, file);
        return Json(new
        {
            success = result.Success,
            hash,
            file,
            diff = result.Output,
            error = result.Error
        });
    }

    [HttpPost("api/git/commit/restore-file")]
    public async Task<IActionResult> RestoreFile([FromBody] RestoreFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.Hash) || string.IsNullOrWhiteSpace(request.File))
        {
            return BadRequest(new { success = false, error = "Path, Hash, and File are required." });
        }

        var result = await _gitService.RestoreFileFromCommitAsync(request.Path, request.Hash, request.File);
        var shortHash = request.Hash.Length >= 7 ? request.Hash[..7] : request.Hash;
        return Json(new
        {
            success = result.Success,
            message = result.Success ? $"Page/file '{request.File}' restored from commit {shortHash} into system." : result.Error
        });
    }

    [HttpPost("api/git/commit/restore-all")]
    public async Task<IActionResult> RestoreAll([FromBody] RestoreCommitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.Hash))
        {
            return BadRequest(new { success = false, error = "Path and Hash are required." });
        }

        var result = await _gitService.RestoreAllFromCommitAsync(request.Path, request.Hash);
        var shortHash = request.Hash.Length >= 7 ? request.Hash[..7] : request.Hash;
        return Json(new
        {
            success = result.Success,
            message = result.Success ? $"All changes from commit {shortHash} restored into system." : result.Error
        });
    }

    [HttpPost("api/git/commit/revert")]
    public async Task<IActionResult> Revert([FromBody] RestoreCommitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.Hash))
        {
            return BadRequest(new { success = false, error = "Path and Hash are required." });
        }

        var result = await _gitService.RevertCommitAsync(request.Path, request.Hash);
        var shortHash = request.Hash.Length >= 7 ? request.Hash[..7] : request.Hash;
        return Json(new
        {
            success = result.Success,
            message = result.Success ? $"Commit {shortHash} successfully reverted in system." : result.Error
        });
    }

    [HttpGet("api/git/commit/file-content")]
    public async Task<IActionResult> GetFileContent([FromQuery] string path, [FromQuery] string hash, [FromQuery] string file)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(file))
        {
            return BadRequest(new { success = false, error = "Path, Hash, and File are required." });
        }

        var commitContentResult = await _gitService.GetFileContentAtCommitAsync(path, hash, file);
        var parentContentResult = await _gitService.GetFileContentAtCommitAsync(path, $"{hash}~1", file);

        string currentContent = string.Empty;
        bool existsOnDisk = false;
        try
        {
            var diskPath = Path.Combine(path, file);
            if (System.IO.File.Exists(diskPath))
            {
                currentContent = await System.IO.File.ReadAllTextAsync(diskPath);
                existsOnDisk = true;
            }
        }
        catch
        {
            // Ignore non-readable or binary file exceptions
        }

        return Json(new
        {
            success = commitContentResult.Success,
            hash,
            file,
            commitContent = commitContentResult.Output ?? string.Empty,
            parentContent = parentContentResult.Success ? (parentContentResult.Output ?? string.Empty) : string.Empty,
            currentContent,
            existsOnDisk,
            error = commitContentResult.Error
        });
    }

    [HttpGet("api/git/commit/download-file")]
    public async Task<IActionResult> DownloadFile([FromQuery] string path, [FromQuery] string hash, [FromQuery] string file)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(file))
        {
            return BadRequest("Path, Hash, and File are required.");
        }

        var contentResult = await _gitService.GetFileContentAtCommitAsync(path, hash, file);
        if (!contentResult.Success)
        {
            return NotFound($"File '{file}' not found in commit {hash}: {contentResult.Error}");
        }

        var fileName = Path.GetFileName(file);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "download.txt";

        var bytes = System.Text.Encoding.UTF8.GetBytes(contentResult.Output ?? string.Empty);
        return File(bytes, "application/octet-stream", fileName);
    }

    [HttpPost("api/git/commit/update-file")]
    public async Task<IActionResult> UpdateFile([FromBody] UpdateFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.File))
        {
            return BadRequest(new { success = false, error = "Path and File are required." });
        }

        try
        {
            var fullRepoPath = Path.GetFullPath(request.Path);
            var fullFilePath = Path.GetFullPath(Path.Combine(request.Path, request.File));

            if (!fullFilePath.StartsWith(fullRepoPath, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, error = "Invalid file path outside repository directory." });
            }

            var dir = Path.GetDirectoryName(fullFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await System.IO.File.WriteAllTextAsync(fullFilePath, request.Content ?? string.Empty, System.Text.Encoding.UTF8);

            return Json(new
            {
                success = true,
                message = $"Page/file '{request.File}' updated successfully in working directory."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
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

public class RestoreFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
}

public class RestoreCommitRequest
{
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}

public class UpdateFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

