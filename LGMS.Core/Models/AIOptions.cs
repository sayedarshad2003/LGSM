namespace LGMS.Models;

/// <summary>
/// General AI options and active provider configuration.
/// </summary>
public class AIOptions
{
    public const string SectionName = "AI";

    /// <summary>
    /// Active AI provider for commit generation ("Gemini", "DeepSeek", etc.).
    /// </summary>
    public string ActiveProvider { get; set; } = "Gemini";

    /// <summary>
    /// Active model override, if any.
    /// </summary>
    public string? ActiveModel { get; set; }

    /// <summary>
    /// Global diff character length limit.
    /// </summary>
    public int MaxDiffLength { get; set; } = 8000;
}
