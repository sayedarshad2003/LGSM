namespace LGMS.Models;

public class GitCommit
{
    public string Hash { get; set; } = string.Empty;
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string RelativeTime { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int FilesChanged { get; set; }
    public List<string> ParentHashes { get; set; } = new();
}
