namespace LGMS.Models;

public sealed class LocalRepository
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string? CurrentBranch { get; set; }
    public DateTime LastScannedAt { get; set; } = DateTime.UtcNow;
}
