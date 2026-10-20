namespace LGMS.Models;

/// <summary>
/// Result of an AI-generated commit message.
/// </summary>
public class AICommitResult
{
    public bool Success { get; set; }

    /// <summary>
    /// Concise conventional commit title (e.g. "feat(git): add DeepSeek commit auto-generation").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed bullet points and explanation of changes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Full commit message formatted as Title + blank line + Description.
    /// </summary>
    public string FullMessage { get; set; } = string.Empty;

    /// <summary>
    /// Error or warning message, if any.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Diagnostic message or warning (e.g., if fallback heuristic was used or diff was truncated).
    /// </summary>
    public string? Warning { get; set; }

    /// <summary>
    /// Indicates whether the local heuristic fallback was used instead of the DeepSeek API.
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// AI provider used (e.g. "Gemini", "DeepSeek", or "Local Heuristic").
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Model name used (e.g. "gemini-2.0-flash", "deepseek-chat", or "local-heuristic").
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Total characters in the git diff analyzed.
    /// </summary>
    public int DiffLength { get; set; }

    /// <summary>
    /// True if the diff exceeded MaxDiffLength and had to be truncated.
    /// </summary>
    public bool DiffTruncated { get; set; }

    /// <summary>
    /// Number of files with changes.
    /// </summary>
    public int ChangedFilesCount { get; set; }

    /// <summary>
    /// First commit hash in the repository, if available.
    /// </summary>
    public string? FirstCommitHash { get; set; }

    /// <summary>
    /// Last commit hash before this commit, if available.
    /// </summary>
    public string? LastCommitHash { get; set; }

    /// <summary>
    /// Subject of the last commit for context reference.
    /// </summary>
    public string? LastCommitSubject { get; set; }

    /// <summary>
    /// Duration of the generation in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    public static AICommitResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
