using LGMS.Models;
using LGMS.Services;

namespace LGMS.ViewModels;

public class GitHubAccountsViewModel
{
    public List<GitHubAccountInfo> Accounts { get; set; } = new();
    public List<GitOperationLog> RecentLogs { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class GitHubRepositoriesViewModel
{
    public string Host { get; set; } = "github.com";
    public string Username { get; set; } = string.Empty;
    public List<GitHubAccountInfo> AllAccounts { get; set; } = new();
    public List<GitHubRepositoryInfo> Repositories { get; set; } = new();
    public HashSet<string> ExistingLocalPaths { get; set; } = new();
    public string DefaultCloneRoot { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class CloneRepositoryViewModel
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string Host { get; set; } = "github.com";
    public string Username { get; set; } = string.Empty;
}

public class CreateRemoteRepositoryViewModel
{
    public string Host { get; set; } = "github.com";
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPrivate { get; set; } = true;
    public string? LocalSourcePath { get; set; }
}
