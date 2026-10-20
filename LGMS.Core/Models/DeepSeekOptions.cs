namespace LGMS.Models;

/// <summary>
/// Configuration options for DeepSeek AI integration.
/// Loaded from appsettings.json section "DeepSeek".
/// </summary>
public class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    /// <summary>
    /// DeepSeek API Key. Can also be set via DEEPSEEK_API_KEY environment variable.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// DeepSeek base API endpoint (default: https://api.deepseek.com).
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>
    /// Model to use for commit message generation (e.g. "deepseek-chat" or "deepseek-reasoner").
    /// </summary>
    public string Model { get; set; } = "deepseek-chat";

    /// <summary>
    /// Maximum completion tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 600;

    /// <summary>
    /// Sampling temperature between 0.0 and 1.0 (lower is more deterministic and concise).
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum character length of the git diff before it is safely truncated to avoid exceeding token limits.
    /// </summary>
    public int MaxDiffLength { get; set; } = 8000;

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Returns true if an API key has been specified.
    /// </summary>
    public bool HasApiKey =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !ApiKey.Contains("YOUR_DEEPSEEK_API_KEY", StringComparison.OrdinalIgnoreCase);
}
