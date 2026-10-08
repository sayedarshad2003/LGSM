using LGMS.Models;

namespace LGMS.ViewModels;

public class DashboardViewModel
{
    public Repository? Repository { get; set; }
    public GitStatus? Status { get; set; }
    public GitCommit? LastCommit { get; set; }
    public List<Repository> RecentRepositories { get; set; } = new();
    public WorkspaceSettings Settings { get; set; } = new();
    public bool HasRepositories { get; set; }
    public int TotalRepositoriesFound { get; set; }
    public bool IsVsCodeInstalled { get; set; }
}

public class RepositoryViewModel
{
    public List<Repository> Repositories { get; set; } = new();
    public string WorkingFolder { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public string SortBy { get; set; } = "name";
}

public class ChangesViewModel
{
    public Repository Repository { get; set; } = new();
    public GitStatus Status { get; set; } = new();
    public string? SelectedFilePath { get; set; }
    public string? DiffContent { get; set; }
    public bool IsDiffStaged { get; set; }
}

public class CommitViewModel
{
    public Repository Repository { get; set; } = new();
    public List<GitChange> StagedChanges { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public bool ShouldPush { get; set; }
}

public class BranchViewModel
{
    public Repository Repository { get; set; } = new();
    public List<GitBranch> LocalBranches { get; set; } = new();
    public List<GitBranch> RemoteBranches { get; set; } = new();
    public string CurrentBranch { get; set; } = string.Empty;
}
