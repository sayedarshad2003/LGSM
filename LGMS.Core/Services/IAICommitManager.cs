using LGMS.Models;

namespace LGMS.Services;

/// <summary>
/// Manager for resolving and orchestrating multiple AI commit message providers (Gemini, DeepSeek, future providers).
/// </summary>
public interface IAICommitManager
{
    /// <summary>
    /// Gets the registered AI commit service by provider name, or default active provider.
    /// </summary>
    IAICommitService GetProvider(string? providerName = null);

    /// <summary>
    /// Returns all registered AI commit providers.
    /// </summary>
    IEnumerable<IAICommitService> GetAllProviders();

    /// <summary>
    /// Returns a dictionary of provider name to supported models.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> GetAvailableModels();

    /// <summary>
    /// Tests connectivity for the specified provider (or active provider) with an optional model override.
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync(
        string? providerName = null,
        string? modelOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns supported models for the specified provider.
    /// </summary>
    IReadOnlyList<string> GetSupportedModels(string providerName);

    /// <summary>
    /// Generates a commit message using the requested or active provider and model.
    /// </summary>
    Task<AICommitResult> GenerateCommitMessageAsync(
        string repositoryPath,
        string? providerName = null,
        string? model = null,
        bool includeUnstaged = true,
        string? customPrompt = null,
        CancellationToken ct = default);
}
