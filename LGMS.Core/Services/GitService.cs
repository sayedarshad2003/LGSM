using LGMS.Models;
using System.Text.RegularExpressions;

namespace LGMS.Services;

public class GitService : IGitService
{
    private readonly IProcessService _processService;
    private readonly ILogger<GitService> _logger;

    public GitService(IProcessService processService, ILogger<GitService> logger)
    {
        _processService = processService;
        _logger = logger;
    }

    private GitResult MapResult(ProcessResult pr) => new()
    {
        Success = pr.Success,
        ExitCode = pr.ExitCode,
        Output = pr.StandardOutput,
        Error = pr.StandardError,
        DurationMs = pr.DurationMs
    };

    public async Task<GitResult> StatusAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "status", "--porcelain=v1" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitStatus> ParseStatusAsync(string repositoryPath)
    {
        var status = new GitStatus { RepositoryPath = repositoryPath };

        // Current branch
        var branchResult = await _processService.ExecuteAsync("git", new[] { "rev-parse", "--abbrev-ref", "HEAD" }, repositoryPath);
        if (branchResult.Success)
        {
            status.CurrentBranch = branchResult.StandardOutput.Trim();
        }

        // Upstream and ahead/behind
        var upstreamResult = await _processService.ExecuteAsync("git", new[] { "rev-parse", "--abbrev-ref", "@{upstream}" }, repositoryPath);
        if (upstreamResult.Success)
        {
            status.UpstreamBranch = upstreamResult.StandardOutput.Trim();
            var countResult = await _processService.ExecuteAsync("git", new[] { "rev-list", "--left-right", "--count", "@{upstream}...HEAD" }, repositoryPath);
            if (countResult.Success)
            {
                var parts = countResult.StandardOutput.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead))
                {
                    status.Behind = behind;
                    status.Ahead = ahead;
                }
            }
        }

        // Last commit
        var logResult = await _processService.ExecuteAsync("git", new[] { "log", "-1", "--format=%h%x09%s" }, repositoryPath);
        if (logResult.Success && !string.IsNullOrWhiteSpace(logResult.StandardOutput))
        {
            var parts = logResult.StandardOutput.Trim().Split('\t');
            if (parts.Length > 0) status.LatestCommitHash = parts[0];
            if (parts.Length > 1) status.LatestCommitSubject = parts[1];
        }

        // Status porcelain
        var statusResult = await StatusAsync(repositoryPath);
        if (!statusResult.Success) return status;

        var lines = statusResult.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            char indexStatus = line[0];
            char workTreeStatus = line[1];
            string filePath = line[3..].Trim();

            string? originalPath = null;
            if (filePath.Contains(" -> "))
            {
                var parts = filePath.Split(" -> ");
                originalPath = parts[0].Trim();
                filePath = parts[1].Trim();
            }

            // Staged change
            if (indexStatus != ' ' && indexStatus != '?')
            {
                status.Changes.Add(new GitChange
                {
                    Path = filePath,
                    OriginalPath = originalPath,
                    IsStaged = true,
                    StatusCode = indexStatus.ToString(),
                    Status = ParseStatusCode(indexStatus)
                });
            }

            // Unstaged change
            if (workTreeStatus != ' ' && workTreeStatus != '?')
            {
                status.Changes.Add(new GitChange
                {
                    Path = filePath,
                    OriginalPath = originalPath,
                    IsStaged = false,
                    StatusCode = workTreeStatus.ToString(),
                    Status = ParseStatusCode(workTreeStatus)
                });
            }

            // Untracked
            if (indexStatus == '?' && workTreeStatus == '?')
            {
                status.Changes.Add(new GitChange
                {
                    Path = filePath,
                    IsStaged = false,
                    StatusCode = "??",
                    Status = ChangeStatus.Untracked
                });
            }
        }

        return status;
    }

    private ChangeStatus ParseStatusCode(char code) => code switch
    {
        'M' => ChangeStatus.Modified,
        'A' => ChangeStatus.Added,
        'D' => ChangeStatus.Deleted,
        'R' => ChangeStatus.Renamed,
        'C' => ChangeStatus.Copied,
        'U' => ChangeStatus.Unmerged,
        'T' => ChangeStatus.TypeChanged,
        '?' => ChangeStatus.Untracked,
        _ => ChangeStatus.Modified
    };

    public async Task<GitResult> DiffAsync(string repositoryPath, bool staged = false, string? filePath = null)
    {
        var args = new List<string> { "diff" };
        if (staged)
        {
            args.Add("--cached");
        }
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            args.Add("--");
            args.Add(filePath);
        }
        var result = await _processService.ExecuteAsync("git", args, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> StageAsync(string repositoryPath, IEnumerable<string> files)
    {
        var fileList = files.ToList();
        var args = new List<string> { "add", "--" };
        if (fileList.Count == 0 || fileList.Contains("."))
        {
            args = new List<string> { "add", "-A" };
        }
        else
        {
            args.AddRange(fileList);
        }
        var result = await _processService.ExecuteAsync("git", args, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> UnstageAsync(string repositoryPath, IEnumerable<string> files)
    {
        var fileList = files.ToList();
        var args = new List<string> { "restore", "--staged", "--" };
        if (fileList.Count == 0 || fileList.Contains("."))
        {
            args.Add(".");
        }
        else
        {
            args.AddRange(fileList);
        }
        var result = await _processService.ExecuteAsync("git", args, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> DiscardAsync(string repositoryPath, IEnumerable<string> files)
    {
        var fileList = files.ToList();
        if (fileList.Count == 0) return GitResult.Fail("No files selected to discard.");

        var args = new List<string> { "restore", "--" };
        args.AddRange(fileList);
        var restoreResult = await _processService.ExecuteAsync("git", args, repositoryPath);

        // Also clean untracked if any
        var cleanArgs = new List<string> { "clean", "-fd", "--" };
        cleanArgs.AddRange(fileList);
        await _processService.ExecuteAsync("git", cleanArgs, repositoryPath);

        return MapResult(restoreResult);
    }

    public async Task<GitResult> CommitAsync(string repositoryPath, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return GitResult.Fail("Commit message cannot be empty.");
        }
        var result = await _processService.ExecuteAsync("git", new[] { "commit", "-m", message.Trim() }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> PushAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "push" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> PushAsync(string repositoryPath, string remote, string branch)
    {
        ValidateGitArg(remote);
        ValidateGitArg(branch);
        var result = await _processService.ExecuteAsync("git", new[] { "push", remote, branch }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> PullAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "pull" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> PullAsync(string repositoryPath, string remote, string branch)
    {
        ValidateGitArg(remote);
        ValidateGitArg(branch);
        var result = await _processService.ExecuteAsync("git", new[] { "pull", "--", remote, branch }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> FetchAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "fetch", "--all", "--prune" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> FetchAsync(string repositoryPath, string remote)
    {
        ValidateGitArg(remote);
        var result = await _processService.ExecuteAsync("git", new[] { "fetch", "--prune", remote }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> CloneAsync(string repositoryUrl, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return GitResult.Fail("Repository URL cannot be empty.");
        }

        var trimmedUrl = repositoryUrl.Trim();
        var targetDest = destinationPath.Trim();

        // If target directory exists and is not empty, check if we can append repository name
        if (Directory.Exists(targetDest) && Directory.EnumerateFileSystemEntries(targetDest).Any())
        {
            var cleanUrl = trimmedUrl.TrimEnd('/');
            var repoName = Path.GetFileNameWithoutExtension(cleanUrl);
            if (!string.IsNullOrWhiteSpace(repoName))
            {
                var combined = Path.Combine(targetDest, repoName);
                if (!Directory.Exists(combined) || !Directory.EnumerateFileSystemEntries(combined).Any())
                {
                    targetDest = combined;
                }
                else
                {
                    return GitResult.Fail($"The clone destination directory '{combined}' already exists and is not empty.");
                }
            }
            else
            {
                return GitResult.Fail("The clone destination directory must be empty.");
            }
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(targetDest));
        if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var result = await _processService.ExecuteAsync(
            "git",
            new[] { "clone", "--", trimmedUrl, targetDest },
            workingDirectory: parent ?? Environment.CurrentDirectory,
            cancellationToken: cancellationToken,
            timeout: TimeSpan.FromMinutes(20));

        var mapped = MapResult(result);
        if (mapped.Success && string.IsNullOrWhiteSpace(mapped.Output))
        {
            mapped.Output = targetDest;
        }
        return mapped;
    }

    public async Task<GitResult> InitAndInitialCommitAsync(string repositoryPath, string commitMessage = "Initial Commit")
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return GitResult.Fail("Repository path cannot be empty.");
        }

        var fullPath = Path.GetFullPath(repositoryPath);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        // 1. git init
        var initResult = await _processService.ExecuteAsync("git", new[] { "init" }, fullPath);
        if (!initResult.Success)
        {
            return MapResult(initResult);
        }

        // 2. git add -A
        var addResult = await _processService.ExecuteAsync("git", new[] { "add", "-A" }, fullPath);
        if (!addResult.Success)
        {
            return MapResult(addResult);
        }

        // 3. Ensure commit author identity is available so git commit doesn't fail if global config is missing
        var configUserCheck = await _processService.ExecuteAsync("git", new[] { "config", "user.name" }, fullPath);
        var commitArgs = new List<string>();
        if (!configUserCheck.Success || string.IsNullOrWhiteSpace(configUserCheck.StandardOutput))
        {
            commitArgs.AddRange(new[] { "-c", "user.name=LGMS User", "-c", "user.email=lgms@local.dev" });
        }

        var msg = string.IsNullOrWhiteSpace(commitMessage) ? "Initial Commit" : commitMessage.Trim();
        commitArgs.AddRange(new[] { "commit", "--allow-empty", "-m", msg });

        var commitResult = await _processService.ExecuteAsync("git", commitArgs, fullPath);
        return MapResult(commitResult);
    }

    public async Task<GitResult> GetBranchesAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "branch", "-a", "--format=%(refname:short)%x09%(HEAD)%x09%(upstream:short)%x09%(objectname:short)%x09%(subject)" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> CheckoutAsync(string repositoryPath, string branch)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "checkout", branch }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> CreateBranchAsync(string repositoryPath, string branch)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "checkout", "-b", branch }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> DeleteBranchAsync(string repositoryPath, string branch, bool force = false)
    {
        var flag = force ? "-D" : "-d";
        var result = await _processService.ExecuteAsync("git", new[] { "branch", flag, branch }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> MergeBranchAsync(string repositoryPath, string branch)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "merge", branch }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> GetCommitsAsync(string repositoryPath, int maxCount = 50)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "log", $"-n{maxCount}", "--format=%H%x09%h%x09%an%x09%ae%x09%aI%x09%ar%x09%s%x09%b" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> GetRemotesAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "remote", "-v" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> AddRemoteAsync(string repositoryPath, string name, string url)
    {
        ValidateGitArg(name);
        var result = await _processService.ExecuteAsync("git", new[] { "remote", "add", name, url }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> RemoveRemoteAsync(string repositoryPath, string name)
    {
        ValidateGitArg(name);
        var result = await _processService.ExecuteAsync("git", new[] { "remote", "remove", name }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> GetStashesAsync(string repositoryPath)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "stash", "list", "--format=%gd%x09%s%x09%cr" }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> StashSaveAsync(string repositoryPath, string? message = null)
    {
        var args = new List<string> { "stash", "push", "--include-untracked" };
        if (!string.IsNullOrWhiteSpace(message))
        {
            args.Add("-m");
            args.Add(message.Trim());
        }
        var result = await _processService.ExecuteAsync("git", args, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> StashApplyAsync(string repositoryPath, string stashRef)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "stash", "apply", stashRef }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> StashPopAsync(string repositoryPath, string stashRef)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "stash", "pop", stashRef }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> StashDropAsync(string repositoryPath, string stashRef)
    {
        var result = await _processService.ExecuteAsync("git", new[] { "stash", "drop", stashRef }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> RunCommandAsync(string repositoryPath, string command)
    {
        var trimmed = command.Trim();
        if (trimmed.StartsWith("git ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..].Trim();
        }

        var matches = Regex.Matches(trimmed, @"[\""].+?[\""]|[^ ]+");
        var args = matches.Select(m => m.Value.Trim('"')).ToArray();

        var result = await _processService.ExecuteAsync("git", args, repositoryPath, timeout: TimeSpan.FromSeconds(30));
        return MapResult(result);
    }

    private static void ValidateGitArg(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('-') || value.Contains('\n') || value.Contains('\r') || value.Contains('\0'))
        {
            throw new ArgumentException("Invalid Git argument.");
        }
    }

    public async Task<List<CommitFileChange>> GetCommitFilesAsync(string repositoryPath, string hash)
    {
        var files = new List<CommitFileChange>();
        if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(hash))
        {
            return files;
        }

        var statusResult = await _processService.ExecuteAsync("git", new[] { "show", "--name-status", "--format=", hash.Trim() }, repositoryPath);
        if (!statusResult.Success || string.IsNullOrWhiteSpace(statusResult.StandardOutput))
        {
            return files;
        }

        var lines = statusResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var map = new Dictionary<string, CommitFileChange>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var statusCode = parts[0].Trim();
                var filePath = parts.Length >= 3 ? parts[2].Trim() : parts[1].Trim();
                var status = statusCode.StartsWith("A") ? ChangeStatus.Added :
                             statusCode.StartsWith("D") ? ChangeStatus.Deleted :
                             statusCode.StartsWith("R") ? ChangeStatus.Renamed : ChangeStatus.Modified;

                var item = new CommitFileChange
                {
                    Path = filePath,
                    StatusCode = statusCode,
                    Status = status
                };
                map[filePath] = item;
                files.Add(item);
            }
        }

        var numstatResult = await _processService.ExecuteAsync("git", new[] { "show", "--numstat", "--format=", hash.Trim() }, repositoryPath);
        if (numstatResult.Success && !string.IsNullOrWhiteSpace(numstatResult.StandardOutput))
        {
            var numLines = numstatResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var nLine in numLines)
            {
                var parts = nLine.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    int.TryParse(parts[0], out var add);
                    int.TryParse(parts[1], out var del);
                    var filePath = parts[2].Trim();
                    if (map.TryGetValue(filePath, out var item))
                    {
                        item.Additions = add;
                        item.Deletions = del;
                    }
                }
            }
        }

        return files;
    }

    public async Task<GitResult> GetCommitFileDiffAsync(string repositoryPath, string hash, string filePath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filePath))
        {
            return GitResult.Fail("Repository path, hash, and file path are required.");
        }

        var result = await _processService.ExecuteAsync("git", new[] { "show", "--patch", hash.Trim(), "--", filePath.Trim() }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> RestoreFileFromCommitAsync(string repositoryPath, string hash, string filePath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filePath))
        {
            return GitResult.Fail("Repository path, hash, and file path are required.");
        }

        var result = await _processService.ExecuteAsync("git", new[] { "checkout", hash.Trim(), "--", filePath.Trim() }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> RestoreAllFromCommitAsync(string repositoryPath, string hash)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(hash))
        {
            return GitResult.Fail("Repository path and hash are required.");
        }

        var result = await _processService.ExecuteAsync("git", new[] { "checkout", hash.Trim(), "--", "." }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> RevertCommitAsync(string repositoryPath, string hash)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(hash))
        {
            return GitResult.Fail("Repository path and hash are required.");
        }

        var result = await _processService.ExecuteAsync("git", new[] { "revert", "--no-edit", hash.Trim() }, repositoryPath);
        return MapResult(result);
    }

    public async Task<GitResult> GetFileContentAtCommitAsync(string repositoryPath, string hash, string filePath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filePath))
        {
            return GitResult.Fail("Repository path, hash, and file path are required.");
        }

        var gitPath = filePath.Trim().Replace('\\', '/');
        var spec = $"{hash.Trim()}:{gitPath}";
        var result = await _processService.ExecuteAsync("git", new[] { "show", spec }, repositoryPath);
        return MapResult(result);
    }
}
