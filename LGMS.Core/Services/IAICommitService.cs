using LGMS.Models;

namespace LGMS.Services;

/// <summary>
/// Generalized interface for an AI Git commit message generation provider (Google Gemini, DeepSeek, OpenAI, etc.).
/// </summary>
public interface IAICommitService
{
    /// <summary>
    /// Unique provider name (e.g. "Gemini", "DeepSeek").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// List of popular or supported model identifiers for this provider.
    /// </summary>
    IReadOnlyList<string> SupportedModels { get; }

    /// <summary>
    /// Generates a conventional commit message (Title + Details) for changes in the specified repository.
    /// </summary>
    /// <param name="repositoryPath">Path to local repository.</param>
    /// <param name="includeUnstaged">Whether to include unstaged changes if staged changes are empty.</param>
    /// <param name="customPrompt">Optional custom hint from user.</param>
    /// <param name="modelOverride">Optional model override (e.g. "gemini-2.0-flash", "deepseek-reasoner").</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AICommitResult> GenerateCommitMessageAsync(
        string repositoryPath,
        bool includeUnstaged = true,
        string? customPrompt = null,
        string? modelOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Tests connectivity and API key validity with the provider.
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync(string? modelOverride = null, CancellationToken ct = default);
}
