namespace LGMS.Models;

public class GitStatus
{
    public string RepositoryPath { get; set; } = string.Empty;
    public string CurrentBranch { get; set; } = string.Empty;
    public string? UpstreamBranch { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public bool IsClean => Changes.Count == 0;
    public List<GitChange> Changes { get; set; } = new();
    public List<GitChange> StagedChanges => Changes.Where(c => c.IsStaged).ToList();
    public List<GitChange> UnstagedChanges => Changes.Where(c => !c.IsStaged && c.Status != ChangeStatus.Untracked).ToList();
    public List<GitChange> UntrackedFiles => Changes.Where(c => c.Status == ChangeStatus.Untracked).ToList();
    public string? LatestCommitHash { get; set; }
    public string? LatestCommitSubject { get; set; }
}
