namespace LGMS.Models;

public class GitStash
{
    public int Index { get; set; }
    public string RefName => $"stash@{{{Index}}}";
    public string Message { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}
