using LGMS.Models;
using Microsoft.Extensions.Options;

namespace LGMS.Services;

/// <summary>
/// Orchestrates multiple AI providers (Google Gemini, DeepSeek, and future providers)
/// for commit message generation, model selection, and fallback routing.
/// </summary>
public class AICommitManager : IAICommitManager
{
    private readonly IEnumerable<IAICommitService> _providers;
    private readonly AIOptions _aiOptions;
    private readonly ILogger<AICommitManager> _logger;

    public AICommitManager(
        IEnumerable<IAICommitService> providers,
        IOptions<AIOptions> aiOptions,
        ILogger<AICommitManager> logger)
    {
        _providers = providers;
        _aiOptions = aiOptions.Value ?? new AIOptions();
        _logger = logger;
    }

    public IAICommitService GetProvider(string? providerName = null)
    {
        var target = string.IsNullOrWhiteSpace(providerName)
            ? (_aiOptions.ActiveProvider ?? "Gemini")
            : providerName;

        var found = _providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, target, StringComparison.OrdinalIgnoreCase));

        if (found != null)
        {
            return found;
        }

        // Fallback to first available provider
        var first = _providers.FirstOrDefault();
        if (first != null)
        {
            _logger.LogWarning("Provider '{Target}' not found. Falling back to '{First}'.", target, first.ProviderName);
            return first;
        }

        throw new InvalidOperationException($"No AI commit providers registered in container.");
    }

    public IEnumerable<IAICommitService> GetAllProviders()
    {
        return _providers;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetAvailableModels()
    {
        return _providers.ToDictionary(
            p => p.ProviderName,
            p => p.SupportedModels,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        string? providerName = null,
        string? modelOverride = null,
        CancellationToken ct = default)
    {
        var provider = GetProvider(providerName);
        return await provider.TestConnectionAsync(modelOverride, ct);
    }

    public IReadOnlyList<string> GetSupportedModels(string providerName)
    {
        var modelsDict = GetAvailableModels();
        if (modelsDict.TryGetValue(providerName, out var models))
        {
            return models;
        }
        return Array.Empty<string>();
    }

    public async Task<AICommitResult> GenerateCommitMessageAsync(
        string repositoryPath,
        string? providerName = null,
        string? model = null,
        bool includeUnstaged = true,
        string? customPrompt = null,
        CancellationToken ct = default)
    {
        var provider = GetProvider(providerName);
        var effectiveModel = !string.IsNullOrWhiteSpace(model)
            ? model
            : _aiOptions.ActiveModel;

        _logger.LogInformation("Generating commit message using AI provider: {Provider}, model: {Model}", provider.ProviderName, effectiveModel ?? "default");

        return await provider.GenerateCommitMessageAsync(
            repositoryPath,
            includeUnstaged,
            customPrompt,
            effectiveModel,
            ct);
    }
}
