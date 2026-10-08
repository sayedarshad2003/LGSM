namespace LGMS.Models;

public class GitRemote
{
    public string Name { get; set; } = "origin";
    public string FetchUrl { get; set; } = string.Empty;
    public string PushUrl { get; set; } = string.Empty;
}
