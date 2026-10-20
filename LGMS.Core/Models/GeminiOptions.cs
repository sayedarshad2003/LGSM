namespace LGMS.Models;

/// <summary>
/// Configuration options for Google Gemini AI integration.
/// Loaded from appsettings.json section "Gemini".
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Google Gemini API Key. Can also be set via GEMINI_API_KEY environment variable.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base API URL for Google Generative Language API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>
    /// Gemini model identifier (e.g. "gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.8-flash", "gemini-3.5-flash").
    /// </summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Maximum completion tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 600;

    /// <summary>
    /// Sampling temperature between 0.0 and 1.0 (lower is more deterministic).
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum character length of the git diff before it is safely truncated.
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
        !ApiKey.Contains("YOUR_GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase);
}
