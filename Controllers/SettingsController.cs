using LGMS.Models;
using LGMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class SettingsController : Controller
{
    private readonly IProcessService _processService;
    private readonly IRepositoryScanner _scanner;
    private readonly IConfiguration _configuration;

    public SettingsController(
        IProcessService processService,
        IRepositoryScanner scanner,
        IConfiguration configuration)
    {
        _processService = processService;
        _scanner = scanner;
        _configuration = configuration;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Index()
    {
        var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        var gitPath = HttpContext.Session.GetString("GitExecutable") ?? "git";
        var theme = HttpContext.Session.GetString("Theme") ?? "dark";

        var activeProvider = HttpContext.Session.GetString("ActiveAiProvider")
            ?? _configuration["AI:ActiveProvider"]
            ?? "Gemini";

        var selectedModel = HttpContext.Session.GetString("SelectedAiModel")
            ?? _configuration["AI:ActiveModel"]
            ?? (string.Equals(activeProvider, "DeepSeek", StringComparison.OrdinalIgnoreCase) ? "deepseek-chat" : "gemini-2.0-flash");

        var gitVersionResult = await _processService.ExecuteAsync(gitPath, new[] { "--version" }, Environment.CurrentDirectory);
        ViewBag.GitVersion = gitVersionResult.Success ? gitVersionResult.StandardOutput.Trim() : "Git not found";

        var settings = new WorkspaceSettings
        {
            WorkingFolder = workingFolder,
            GitExecutable = gitPath,
            Theme = theme,
            RestoreLastRepository = true,
            AutoScanRepositories = true,
            RememberWorkingFolder = true,
            ActiveAiProvider = activeProvider,
            SelectedAiModel = selectedModel
        };

        return View(settings);
    }

    [HttpPost("api/settings/detect-git")]
    public async Task<IActionResult> DetectGit()
    {
        var candidates = new List<string>
        {
            "git",
            @"C:\Program Files\Git\cmd\git.exe",
            @"C:\Program Files\Git\bin\git.exe",
            "/usr/bin/git",
            "/usr/local/bin/git"
        };

        foreach (var candidate in candidates)
        {
            var result = await _processService.ExecuteAsync(candidate, new[] { "--version" }, Environment.CurrentDirectory);
            if (result.Success)
            {
                return Json(new { success = true, path = candidate, version = result.StandardOutput.Trim() });
            }
        }

        return Json(new { success = false, message = "Could not locate Git automatically. Please specify manual path." });
    }

    [HttpPost("api/settings/save")]
    public IActionResult Save([FromBody] WorkspaceSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.WorkingFolder))
        {
            HttpContext.Session.SetString("WorkingFolder", _scanner.NormalizePath(settings.WorkingFolder));
        }
        if (!string.IsNullOrWhiteSpace(settings.GitExecutable))
        {
            HttpContext.Session.SetString("GitExecutable", settings.GitExecutable.Trim());
        }
        if (!string.IsNullOrWhiteSpace(settings.Theme))
        {
            HttpContext.Session.SetString("Theme", settings.Theme.Trim());
        }
        if (!string.IsNullOrWhiteSpace(settings.ActiveAiProvider))
        {
            HttpContext.Session.SetString("ActiveAiProvider", settings.ActiveAiProvider.Trim());
        }
        if (!string.IsNullOrWhiteSpace(settings.SelectedAiModel))
        {
            HttpContext.Session.SetString("SelectedAiModel", settings.SelectedAiModel.Trim());
        }

        return Json(new { success = true, settings });
    }

    [HttpPost("api/settings/test-deepseek")]
    public async Task<IActionResult> TestDeepSeek(
        [FromServices] IDeepSeekCommitService deepSeekService,
        [FromQuery] string? model,
        CancellationToken ct)
    {
        var (success, message) = await deepSeekService.TestConnectionAsync(model, ct);
        return Json(new { success, message });
    }

    [HttpPost("api/settings/test-gemini")]
    public async Task<IActionResult> TestGemini(
        [FromServices] IEnumerable<IAICommitService> providers,
        [FromQuery] string? model,
        CancellationToken ct)
    {
        var gemini = providers.FirstOrDefault(p => string.Equals(p.ProviderName, "Gemini", StringComparison.OrdinalIgnoreCase));
        if (gemini == null)
        {
            return Json(new { success = false, message = "Gemini AI provider is not registered." });
        }

        var (success, message) = await gemini.TestConnectionAsync(model, ct);
        return Json(new { success, message });
    }
}
