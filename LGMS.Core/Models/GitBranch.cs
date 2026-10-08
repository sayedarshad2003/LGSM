namespace LGMS.Models;

public class GitBranch
{
    public string Name { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsRemote { get; set; }
    public string? Upstream { get; set; }
    public string? LatestCommitHash { get; set; }
    public string? LatestCommitSubject { get; set; }
}
