namespace LGMS.Models;

public class CommitFileChange
{
    public string Path { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Directory => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
    public string StatusCode { get; set; } = "M";
    public ChangeStatus Status { get; set; } = ChangeStatus.Modified;
    public int Additions { get; set; }
    public int Deletions { get; set; }
}
