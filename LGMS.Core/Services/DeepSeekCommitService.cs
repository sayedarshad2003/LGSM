using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LGMS.Models;
using Microsoft.Extensions.Options;

namespace LGMS.Services;

/// <summary>
/// Service that interfaces with the DeepSeek API to auto-generate high-quality conventional Git commit messages
/// based on repository diffs, file changes, and commit history context (first commit vs last commit).
/// </summary>
public class DeepSeekCommitService : IDeepSeekCommitService, IAICommitService
{
    public string ProviderName => "DeepSeek";
    public IReadOnlyList<string> SupportedModels { get; } = new[] { "deepseek-chat", "deepseek-reasoner" };

    private readonly IProcessService _processService;
    private readonly IGitService _gitService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<DeepSeekCommitService> _logger;

    public DeepSeekCommitService(
        IProcessService processService,
        IGitService gitService,
        IHttpClientFactory httpClientFactory,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekCommitService> logger)
    {
        _processService = processService;
        _gitService = gitService;
        _httpClientFactory = httpClientFactory;
        _options = options.Value ?? new DeepSeekOptions();
        _logger = logger;
    }

    public async Task<AICommitResult> GenerateCommitMessageAsync(
        string repositoryPath,
        bool includeUnstaged = true,
        string? customPrompt = null,
        CancellationToken ct = default)
    {
        return await GenerateCommitMessageAsync(repositoryPath, includeUnstaged, customPrompt, null, ct);
    }

    public async Task<AICommitResult> GenerateCommitMessageAsync(
        string repositoryPath,
        bool includeUnstaged = true,
        string? customPrompt = null,
        string? modelOverride = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            return AICommitResult.Fail("Repository path does not exist.");
        }

        try
        {
            // 1. Gather repository status & changes
            var status = await _gitService.ParseStatusAsync(repositoryPath);
            var repoName = Path.GetFileName(repositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // 2. Determine diff content (prefer staged; fallback to working tree if none staged)
            string diffText = string.Empty;
            string diffStat = string.Empty;
            bool isStagedDiff = status.StagedChanges.Count > 0;

            if (isStagedDiff)
            {
                var stagedDiffResult = await _processService.ExecuteAsync("git", new[] { "diff", "--cached" }, repositoryPath, ct);
                diffText = stagedDiffResult.StandardOutput;

                var stagedStatResult = await _processService.ExecuteAsync("git", new[] { "diff", "--cached", "--stat" }, repositoryPath, ct);
                diffStat = stagedStatResult.StandardOutput;
            }
            else if (includeUnstaged && (status.UnstagedChanges.Count > 0 || status.UntrackedFiles.Count > 0))
            {
                var unstagedDiffResult = await _processService.ExecuteAsync("git", new[] { "diff" }, repositoryPath, ct);
                diffText = unstagedDiffResult.StandardOutput;

                var unstagedStatResult = await _processService.ExecuteAsync("git", new[] { "diff", "--stat" }, repositoryPath, ct);
                diffStat = unstagedStatResult.StandardOutput;
            }

            // Check if there are any changes at all
            if (string.IsNullOrWhiteSpace(diffText) && status.UntrackedFiles.Count == 0 && status.StagedChanges.Count == 0 && status.UnstagedChanges.Count == 0)
            {
                return AICommitResult.Fail("No changes detected in repository to commit. Please stage or modify files first.");
            }

            // 3. Inspect Commit History Context: First Commit vs Last Commit
            string? firstCommitHash = null;
            string? lastCommitHash = status.LatestCommitHash;
            string? lastCommitSubject = status.LatestCommitSubject;
            string recentCommitsLog = string.Empty;

            var headCheck = await _processService.ExecuteAsync("git", new[] { "rev-parse", "--verify", "HEAD" }, repositoryPath, ct);
            bool hasHead = headCheck.Success;

            if (hasHead)
            {
                // First commit in repo (root commit)
                var firstCommitResult = await _processService.ExecuteAsync("git", new[] { "rev-list", "--max-parents=0", "HEAD" }, repositoryPath, ct);
                if (firstCommitResult.Success && !string.IsNullOrWhiteSpace(firstCommitResult.StandardOutput))
                {
                    firstCommitHash = firstCommitResult.StandardOutput.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                }

                // Recent 5 commits for style and history context
                var recentCommitsResult = await _processService.ExecuteAsync("git", new[] { "log", "-n", "5", "--oneline" }, repositoryPath, ct);
                if (recentCommitsResult.Success)
                {
                    recentCommitsLog = recentCommitsResult.StandardOutput.Trim();
                }
            }

            // 4. Build changed files summary
            var allChanges = isStagedDiff
                ? status.StagedChanges
                : (status.UnstagedChanges.Any() ? status.UnstagedChanges : status.Changes);

            var changedFilesList = allChanges.Select(c => $"{c.StatusCode} {c.Path}").ToList();
            if (!isStagedDiff && status.UntrackedFiles.Any())
            {
                changedFilesList.AddRange(status.UntrackedFiles.Select(u => $"?? {u.Path}"));
            }

            int changedFilesCount = changedFilesList.Count;

            // 5. Apply Limitations & Safeguards (Diff Truncation)
            int maxDiffLength = _options.MaxDiffLength > 1000 ? _options.MaxDiffLength : 8000;
            int originalDiffLength = diffText.Length;
            bool diffTruncated = false;

            if (diffText.Length > maxDiffLength)
            {
                diffTruncated = true;
                // Truncate cleanly at a line break
                int cutoff = diffText.LastIndexOf('\n', maxDiffLength);
                if (cutoff <= 0) cutoff = maxDiffLength;
                diffText = diffText[..cutoff] + $"\n\n[... Diff truncated: showing first {cutoff} of {originalDiffLength} characters to adhere to token limits ...]";
            }

            // 6. Check DeepSeek API Key configuration
            string apiKey = ResolveApiKey();

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("YOUR_DEEPSEEK_API_KEY", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("DeepSeek API key is not configured. Falling back to local smart heuristic commit generator.");

                var fallbackResult = GenerateLocalHeuristicCommit(
                    repoName,
                    status.CurrentBranch,
                    changedFilesList,
                    diffText,
                    lastCommitSubject);

                fallbackResult.FirstCommitHash = firstCommitHash;
                fallbackResult.LastCommitHash = lastCommitHash;
                fallbackResult.LastCommitSubject = lastCommitSubject;
                fallbackResult.DiffLength = originalDiffLength;
                fallbackResult.DiffTruncated = diffTruncated;
                fallbackResult.ChangedFilesCount = changedFilesCount;
                fallbackResult.DurationMs = sw.ElapsedMilliseconds;
                fallbackResult.UsedFallback = true;
                fallbackResult.Warning = "DeepSeek API key is not configured in appsettings.json. A local smart heuristic generated this message. Add your API key in appsettings.json under 'DeepSeek:ApiKey' to enable DeepSeek AI generation.";

                return fallbackResult;
            }

            // 7. Request Commit Message from DeepSeek API
            var aiResult = await CallDeepSeekApiAsync(
                apiKey,
                repoName,
                status.CurrentBranch,
                firstCommitHash,
                lastCommitHash,
                lastCommitSubject,
                recentCommitsLog,
                changedFilesList,
                diffStat,
                diffText,
                customPrompt,
                modelOverride,
                ct);

            aiResult.FirstCommitHash = firstCommitHash;
            aiResult.LastCommitHash = lastCommitHash;
            aiResult.LastCommitSubject = lastCommitSubject;
            aiResult.DiffLength = originalDiffLength;
            aiResult.DiffTruncated = diffTruncated;
            aiResult.ChangedFilesCount = changedFilesCount;
            aiResult.DurationMs = sw.ElapsedMilliseconds;

            if (diffTruncated && string.IsNullOrEmpty(aiResult.Warning))
            {
                aiResult.Warning = $"Note: Git diff was large ({originalDiffLength:N0} chars) and was safely truncated to {maxDiffLength:N0} chars to stay within token limits.";
            }

            return aiResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI commit message for {Repo}", repositoryPath);

            // In case of error, provide graceful fallback
            var status = await _gitService.ParseStatusAsync(repositoryPath);
            var repoName = Path.GetFileName(repositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var fallback = GenerateLocalHeuristicCommit(
                repoName,
                status.CurrentBranch,
                status.Changes.Select(c => $"{c.StatusCode} {c.Path}").ToList(),
                string.Empty,
                status.LatestCommitSubject);

            fallback.UsedFallback = true;
            fallback.Error = $"DeepSeek AI error: {ex.Message}. Fallback commit message generated.";
            fallback.DurationMs = sw.ElapsedMilliseconds;
            return fallback;
        }
    }

    private async Task<AICommitResult> CallDeepSeekApiAsync(
        string apiKey,
        string repoName,
        string branchName,
        string? firstCommitHash,
        string? lastCommitHash,
        string? lastCommitSubject,
        string recentCommitsLog,
        List<string> changedFilesList,
        string diffStat,
        string diffText,
        string? customPrompt,
        string? modelOverride,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds > 5 ? _options.TimeoutSeconds : 30);

        var baseUrl = (_options.BaseUrl ?? "https://api.deepseek.com").TrimEnd('/');
        var endpoint = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/chat/completions";

        var systemPrompt = @"You are an expert Git commit message generator. Analyze the provided Git changes, changed files, and repository commit history context.
Output format requirements:
1. Line 1: Conventional commit title: <type>(<scope>): <concise subject> (max 72 characters, lowercase imperative mood, no ending period).
   Allowed types: feat, fix, docs, style, refactor, perf, test, build, ci, chore.
   Scope should reflect the primary module or feature changed (e.g. git, ui, auth, controllers).
2. Line 2: Empty line.
3. Lines 3+: 2 to 5 bullet points starting with '- ' explaining WHAT changes were made and WHY. Focus on meaningful impact rather than trivial file listings.
4. Output ONLY the raw commit message text.
5. NEVER include markdown code blocks (```), greetings, or conversational preamble (e.g., do NOT write 'Here is your commit message:').";

        var userPromptBuilder = new StringBuilder();
        userPromptBuilder.AppendLine($"Repository: {repoName}");
        userPromptBuilder.AppendLine($"Branch: {branchName}");

        if (!string.IsNullOrWhiteSpace(firstCommitHash))
        {
            userPromptBuilder.AppendLine($"First/Initial Commit in repo: {firstCommitHash[..Math.Min(8, firstCommitHash.Length)]}");
        }
        else
        {
            userPromptBuilder.AppendLine("First/Initial Commit: None (This will be the initial commit in repository)");
        }

        if (!string.IsNullOrWhiteSpace(lastCommitHash))
        {
            userPromptBuilder.AppendLine($"Last Commit before this: {lastCommitHash[..Math.Min(8, lastCommitHash.Length)]} - {lastCommitSubject}");
        }

        if (!string.IsNullOrWhiteSpace(recentCommitsLog))
        {
            userPromptBuilder.AppendLine("Recent Commit Style Reference:");
            userPromptBuilder.AppendLine(recentCommitsLog);
        }

        userPromptBuilder.AppendLine("\nChanged Files Summary:");
        foreach (var file in changedFilesList.Take(30))
        {
            userPromptBuilder.AppendLine($"- {file}");
        }
        if (changedFilesList.Count > 30)
        {
            userPromptBuilder.AppendLine($"... and {changedFilesList.Count - 30} more files");
        }

        if (!string.IsNullOrWhiteSpace(diffStat))
        {
            userPromptBuilder.AppendLine("\nGit Diff Stat:");
            userPromptBuilder.AppendLine(diffStat);
        }

        if (!string.IsNullOrWhiteSpace(diffText))
        {
            userPromptBuilder.AppendLine("\nGit Diff Content:");
            userPromptBuilder.AppendLine(diffText);
        }

        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            userPromptBuilder.AppendLine($"\nUser Hint/Requirement: {customPrompt}");
        }

        var selectedModel = !string.IsNullOrWhiteSpace(modelOverride)
            ? modelOverride
            : (string.IsNullOrWhiteSpace(_options.Model) ? "deepseek-chat" : _options.Model);

        var requestBody = new
        {
            model = selectedModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPromptBuilder.ToString() }
            },
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens > 50 ? _options.MaxTokens : 600,
            stream = false
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = jsonContent
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        _logger.LogInformation("Sending request to DeepSeek API endpoint: {Endpoint} with model: {Model}", endpoint, requestBody.model);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "DeepSeek API HTTP connection failure.");
            throw new Exception($"Cannot connect to DeepSeek API ({ex.Message}). Check your internet connection.");
        }
        catch (TaskCanceledException)
        {
            throw new Exception($"DeepSeek API request timed out after {_options.TimeoutSeconds} seconds.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("DeepSeek API returned error status {Status}: {Body}", response.StatusCode, responseBody);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception("DeepSeek API authentication failed (401 Unauthorized). Please check your API key in appsettings.json.");
            }
            if ((int)response.StatusCode == 429)
            {
                throw new Exception("DeepSeek rate limit reached or quota exceeded (429). Please check your DeepSeek account balance.");
            }

            throw new Exception($"DeepSeek API error ({response.StatusCode}): {responseBody}");
        }

        var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekChatResponse>(responseBody);
        var rawContent = deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new Exception("DeepSeek returned an empty response.");
        }

        // Clean any markdown code blocks or wrapper
        rawContent = CleanMarkdownOutput(rawContent);

        // Split Title & Description
        var (title, description) = ParseCommitMessage(rawContent);

        return new AICommitResult
        {
            Success = true,
            Title = title,
            Description = description,
            FullMessage = $"{title}\n\n{description}".Trim(),
            Provider = ProviderName,
            Model = requestBody.model,
            UsedFallback = false
        };
    }

    public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        return TestConnectionAsync(null, ct);
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(string? modelOverride = null, CancellationToken ct = default)
    {
        string apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (false, "DeepSeek API key is not configured in appsettings.json.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var baseUrl = (_options.BaseUrl ?? "https://api.deepseek.com").TrimEnd('/');
            var endpoint = $"{baseUrl}/chat/completions";

            var selectedModel = !string.IsNullOrWhiteSpace(modelOverride)
                ? modelOverride
                : (string.IsNullOrWhiteSpace(_options.Model) ? "deepseek-chat" : _options.Model);

            var requestBody = new
            {
                model = selectedModel,
                messages = new[]
                {
                    new { role = "user", content = "ping" }
                },
                max_tokens = 5
            };

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return (true, $"Successfully connected to DeepSeek API using model '{requestBody.model}'!");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return (false, "DeepSeek API Key is invalid (401 Unauthorized). Please check your key.");
            }

            var err = await response.Content.ReadAsStringAsync(ct);
            return (false, $"DeepSeek API error ({response.StatusCode}): {err}");
        }
        catch (Exception ex)
        {
            return (false, $"Connection error: {ex.Message}");
        }
    }

    public void UpdateApiKey(string apiKey)
    {
        _options.ApiKey = apiKey ?? string.Empty;
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey) &&
            !_options.ApiKey.Contains("YOUR_DEEPSEEK_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            return _options.ApiKey.Trim();
        }

        var envKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey.Trim();
        }

        return string.Empty;
    }

    private static string CleanMarkdownOutput(string text)
    {
        var cleaned = text.Trim();

        // Strip ```markdown or ```git or ``` wrapper
        if (cleaned.StartsWith("```"))
        {
            var firstNewLine = cleaned.IndexOf('\n');
            if (firstNewLine > 0)
            {
                cleaned = cleaned[(firstNewLine + 1)..];
            }
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned[..^3].Trim();
            }
        }

        // Remove conversational prefixes like "Here is the commit message:"
        var introRegex = new Regex(@"^(here\s+is\s+(the\s+)?(suggested\s+)?commit\s+message:?|commit\s+message:?)\s*", RegexOptions.IgnoreCase);
        cleaned = introRegex.Replace(cleaned, "").Trim();

        return cleaned;
    }

    private static (string Title, string Description) ParseCommitMessage(string raw)
    {
        var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0) return ("chore: update repository changes", string.Empty);

        var title = lines[0].Trim();
        // Remove trailing period from conventional commit title if present
        if (title.EndsWith('.')) title = title[..^1];

        var remaining = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();
        var description = string.Join("\n", remaining);

        return (title, description);
    }

    /// <summary>
    /// Smart local heuristic commit message generator when DeepSeek API is not configured or offline.
    /// </summary>
    private static AICommitResult GenerateLocalHeuristicCommit(
        string repoName,
        string branch,
        List<string> changedFiles,
        string diff,
        string? lastCommitSubject)
    {
        string scope = "repo";
        string type = "feat";
        var bullets = new List<string>();

        if (changedFiles.Count == 0)
        {
            return new AICommitResult
            {
                Success = true,
                Title = "chore: update repository files",
                Description = "- Update project files",
                FullMessage = "chore: update repository files\n\n- Update project files",
                UsedFallback = true,
                Model = "local-heuristic"
            };
        }

        // Determine scope from common directory
        var directories = changedFiles
            .Select(f => f.Length > 3 ? f[3..] : f)
            .Select(f => Path.GetDirectoryName(f))
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        if (directories.Any())
        {
            var topDir = directories
                .GroupBy(d => d!.Split(new[] { '/', '\\' })[0])
                .OrderByDescending(g => g.Count())
                .First().Key;
            scope = topDir.ToLowerInvariant();
        }

        // Determine type based on actions & file types
        bool hasDeletions = changedFiles.Any(f => f.StartsWith("D ") || f.StartsWith(" D"));
        bool hasOnlyNew = changedFiles.All(f => f.StartsWith("A ") || f.StartsWith("??"));
        bool hasTests = changedFiles.Any(f => f.Contains("Test", StringComparison.OrdinalIgnoreCase));
        bool hasDocs = changedFiles.Any(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || f.Contains("doc", StringComparison.OrdinalIgnoreCase));

        if (hasDocs && changedFiles.Count == 1)
        {
            type = "docs";
        }
        else if (hasTests)
        {
            type = "test";
        }
        else if (hasOnlyNew)
        {
            type = "feat";
        }
        else if (hasDeletions && changedFiles.Count < 3)
        {
            type = "refactor";
        }
        else
        {
            type = "feat";
        }

        // Build bullet points
        foreach (var file in changedFiles.Take(6))
        {
            var cleanPath = file.Length > 3 ? file[3..] : file;
            var action = file[..Math.Min(2, file.Length)].Trim();

            string actionText = action switch
            {
                "M" => "Update",
                "A" => "Add",
                "D" => "Remove",
                "R" => "Rename",
                "??" => "Add new file",
                _ => "Modify"
            };

            bullets.Add($"- {actionText} {cleanPath}");
        }

        if (changedFiles.Count > 6)
        {
            bullets.Add($"- And {changedFiles.Count - 6} other modified files");
        }

        var sampleFile = changedFiles.First();
        var sampleFileName = Path.GetFileName(sampleFile.Length > 3 ? sampleFile[3..] : sampleFile);
        var title = $"{type}({scope}): update {sampleFileName} and related changes";

        var description = string.Join("\n", bullets);

        return new AICommitResult
        {
            Success = true,
            Title = title,
            Description = description,
            FullMessage = $"{title}\n\n{description}",
            Model = "local-heuristic",
            UsedFallback = true
        };
    }

    #region DeepSeek API Data Contracts
    private class DeepSeekChatResponse
    {
        [JsonPropertyName("choices")]
        public List<DeepSeekChoice>? Choices { get; set; }
    }

    private class DeepSeekChoice
    {
        [JsonPropertyName("message")]
        public DeepSeekMessage? Message { get; set; }
    }

    private class DeepSeekMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
    #endregion
}
