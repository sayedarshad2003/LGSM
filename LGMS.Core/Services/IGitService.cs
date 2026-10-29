using LGMS.Models;

namespace LGMS.Services;

public interface IGitService
{
    Task<GitResult> StatusAsync(string repositoryPath);
    Task<GitResult> DiffAsync(string repositoryPath, bool staged = false, string? filePath = null);
    Task<GitResult> StageAsync(string repositoryPath, IEnumerable<string> files);
    Task<GitResult> UnstageAsync(string repositoryPath, IEnumerable<string> files);
    Task<GitResult> DiscardAsync(string repositoryPath, IEnumerable<string> files);
    Task<GitResult> CommitAsync(string repositoryPath, string message);
    Task<GitResult> PushAsync(string repositoryPath);
    Task<GitResult> PushAsync(string repositoryPath, string remote, string branch);
    Task<GitResult> PullAsync(string repositoryPath);
    Task<GitResult> PullAsync(string repositoryPath, string remote, string branch);
    Task<GitResult> FetchAsync(string repositoryPath);
    Task<GitResult> FetchAsync(string repositoryPath, string remote);
    Task<GitResult> CloneAsync(string repositoryUrl, string destinationPath, CancellationToken cancellationToken = default);
    Task<GitResult> InitAndInitialCommitAsync(string repositoryPath, string commitMessage = "Initial Commit");
    Task<GitResult> GetBranchesAsync(string repositoryPath);
    Task<GitResult> CheckoutAsync(string repositoryPath, string branch);
    Task<GitResult> CreateBranchAsync(string repositoryPath, string branch);
    Task<GitResult> DeleteBranchAsync(string repositoryPath, string branch, bool force = false);
    Task<GitResult> MergeBranchAsync(string repositoryPath, string branch);
    Task<GitResult> GetCommitsAsync(string repositoryPath, int maxCount = 50);
    Task<GitResult> GetRemotesAsync(string repositoryPath);
    Task<GitResult> AddRemoteAsync(string repositoryPath, string name, string url);
    Task<GitResult> RemoveRemoteAsync(string repositoryPath, string name);
    Task<GitResult> GetStashesAsync(string repositoryPath);
    Task<GitResult> StashSaveAsync(string repositoryPath, string? message = null);
    Task<GitResult> StashApplyAsync(string repositoryPath, string stashRef);
    Task<GitResult> StashPopAsync(string repositoryPath, string stashRef);
    Task<GitResult> StashDropAsync(string repositoryPath, string stashRef);
    Task<GitResult> RunCommandAsync(string repositoryPath, string command);
    Task<GitStatus> ParseStatusAsync(string repositoryPath);
    Task<List<CommitFileChange>> GetCommitFilesAsync(string repositoryPath, string hash);
    Task<GitResult> GetCommitFileDiffAsync(string repositoryPath, string hash, string filePath);
    Task<GitResult> RestoreFileFromCommitAsync(string repositoryPath, string hash, string filePath);
    Task<GitResult> RestoreAllFromCommitAsync(string repositoryPath, string hash);
    Task<GitResult> RevertCommitAsync(string repositoryPath, string hash);
    Task<GitResult> GetFileContentAtCommitAsync(string repositoryPath, string hash, string filePath);
}
