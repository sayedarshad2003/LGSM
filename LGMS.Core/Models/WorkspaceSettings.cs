namespace LGMS.Models;

public class WorkspaceSettings
{
    public string WorkingFolder { get; set; } = string.Empty;
    public string GitExecutable { get; set; } = "git";
    public string Theme { get; set; } = "dark"; // "dark", "light", "system"
    public bool RestoreLastRepository { get; set; } = true;
    public bool AutoScanRepositories { get; set; } = true;
    public bool RememberWorkingFolder { get; set; } = true;
    public string? LastSelectedRepositoryPath { get; set; }
    public string ActiveAiProvider { get; set; } = "Gemini";
    public string SelectedAiModel { get; set; } = "gemini-3.5-flash-lite";
    public string? GeminiApiKey { get; set; }
    public string? DeepSeekApiKey { get; set; }
}
