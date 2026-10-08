using LGMS.Models;

namespace LGMS.Services;

public interface IRepositoryScanner
{
    Task<List<Repository>> ScanAsync(string rootPath, CancellationToken cancellationToken = default);
    bool IsPathAllowed(string rootPath, string targetPath);
    string NormalizePath(string path);
    Task<Repository?> GetRepositoryDetailsAsync(string repoPath, CancellationToken cancellationToken = default);
}
