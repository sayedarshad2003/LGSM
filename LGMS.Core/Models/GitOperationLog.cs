namespace LGMS.Models;

public sealed class GitOperationLog
{
    public long Id { get; set; }
    public int? RepositoryId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
