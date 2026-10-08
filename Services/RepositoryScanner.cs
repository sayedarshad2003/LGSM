using LGMS.Models;
using System.Security.Cryptography;
using System.Text;

namespace LGMS.Services;

public class RepositoryScanner : IRepositoryScanner
{
    private readonly IProcessService _processService;
    private readonly ILogger<RepositoryScanner> _logger;

    public RepositoryScanner(IProcessService processService, ILogger<RepositoryScanner> logger)
    {
        _processService = processService;
        _logger = logger;
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var full = Path.GetFullPath(path.Trim());
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public bool IsPathAllowed(string rootPath, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(targetPath)) return false;
        var normalizedRoot = NormalizePath(rootPath);
        var normalizedTarget = NormalizePath(targetPath);

        // Disallow root drives
        var rootDrive = Path.GetPathRoot(normalizedRoot);
        if (normalizedRoot.Equals(rootDrive, StringComparison.OrdinalIgnoreCase))
        {
            // Scanning whole drive root is disallowed for safety
            return false;
        }

        return normalizedTarget.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedTarget.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<Repository>> ScanAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var repos = new List<Repository>();
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return repos;
        }

        var normalizedRoot = NormalizePath(rootPath);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(normalizedRoot);

        await Task.Run(async () =>
        {
            while (queue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var currentDir = queue.Dequeue();
                if (!visited.Add(currentDir)) continue;

                // Check if current directory is a git repository (.git folder or .git file for worktree)
                var gitPath = Path.Combine(currentDir, ".git");
                bool isGitRepo = Directory.Exists(gitPath) || File.Exists(gitPath);

                if (isGitRepo)
                {
                    try
                    {
                        var repo = await GetRepositoryDetailsAsync(currentDir, cancellationToken);
                        if (repo != null)
                        {
                            repos.Add(repo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load repo details for {Path}", currentDir);
                    }
                    // Do not descend into .git or subfolders of a normal repo unless needed
                    continue;
                }

                // Enumerate subdirectories safely
                try
                {
                    var subdirs = Directory.EnumerateDirectories(currentDir);
                    foreach (var sub in subdirs)
                    {
                        var dirName = Path.GetFileName(sub);
                        // Skip system, build, node_modules, and hidden folders
                        if (dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals(".vs", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        queue.Enqueue(sub);
                    }
                }
                catch (UnauthorizedAccessException) { /* Ignore unauthorized folders */ }
                catch (DirectoryNotFoundException) { /* Ignore transient folder delete */ }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Directory traversal error at {Path}", currentDir);
                }
            }
        }, cancellationToken);

        return repos.OrderBy(r => r.Name).ToList();
    }

    public async Task<Repository?> GetRepositoryDetailsAsync(string repoPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(repoPath)) return null;

        var repoName = Path.GetFileName(repoPath);
        if (string.IsNullOrEmpty(repoName)) repoName = repoPath;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(repoPath));
        var id = Convert.ToHexString(hashBytes)[..12].ToLowerInvariant();

        var repo = new Repository
        {
            Id = id,
            Name = repoName,
            Path = repoPath,
            CurrentBranch = "main",
            IsClean = true,
            LastAccessed = DateTime.UtcNow
        };

        // Query branch
        var branchResult = await _processService.ExecuteAsync("git", new[] { "rev-parse", "--abbrev-ref", "HEAD" }, repoPath, cancellationToken);
        if (branchResult.Success)
        {
            repo.CurrentBranch = branchResult.StandardOutput.Trim();
        }

        // Query status porcelain
        var statusResult = await _processService.ExecuteAsync("git", new[] { "status", "--porcelain=v1" }, repoPath, cancellationToken);
        if (statusResult.Success)
        {
            var lines = statusResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            repo.ChangesCount = lines.Length;
            repo.IsClean = lines.Length == 0;
        }

        // Query ahead / behind
        var revListResult = await _processService.ExecuteAsync("git", new[] { "rev-list", "--left-right", "--count", "@{upstream}...HEAD" }, repoPath, cancellationToken);
        if (revListResult.Success)
        {
            var parts = revListResult.StandardOutput.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead))
            {
                repo.BehindCount = behind;
                repo.AheadCount = ahead;
            }
        }

        // Query last commit
        var logResult = await _processService.ExecuteAsync("git", new[] { "log", "-1", "--format=%h%x09%s" }, repoPath, cancellationToken);
        if (logResult.Success && !string.IsNullOrWhiteSpace(logResult.StandardOutput))
        {
            var parts = logResult.StandardOutput.Trim().Split('\t');
            if (parts.Length > 0) repo.LastCommitHash = parts[0];
            if (parts.Length > 1) repo.LastCommitMessage = parts[1];
        }

        return repo;
    }
}
