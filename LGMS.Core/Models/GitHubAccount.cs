namespace LGMS.Models;

public sealed class GitHubAccount
{
    public int Id { get; set; }
    public string Host { get; set; } = "github.com";
    public string Username { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime LastVerifiedAt { get; set; } = DateTime.UtcNow;
}
