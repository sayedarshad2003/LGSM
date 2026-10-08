namespace LGMS.Models;

public class Repository
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string CurrentBranch { get; set; } = "main";
    public bool IsClean { get; set; } = true;
    public int ChangesCount { get; set; }
    public int AheadCount { get; set; }
    public int BehindCount { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? LastAccessed { get; set; }
    public string? LastCommitMessage { get; set; }
    public string? LastCommitHash { get; set; }
}
