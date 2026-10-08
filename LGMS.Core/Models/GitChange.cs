namespace LGMS.Models;

public enum ChangeStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    TypeChanged,
    Copied,
    Unmerged
}

public class GitChange
{
    public string Path { get; set; } = string.Empty;
    public string? OriginalPath { get; set; }
    public ChangeStatus Status { get; set; }
    public bool IsStaged { get; set; }
    public string StatusCode { get; set; } = string.Empty; // e.g. "M", "A", "D", "??"
}
