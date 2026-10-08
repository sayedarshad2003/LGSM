using LGMS.Models;
using LGMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class SettingsController : Controller
{
    private readonly IProcessService _processService;
    private readonly IRepositoryScanner _scanner;

    public SettingsController(IProcessService processService, IRepositoryScanner scanner)
    {
        _processService = processService;
        _scanner = scanner;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Index()
    {
        var workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        var gitPath = HttpContext.Session.GetString("GitExecutable") ?? "git";
        var theme = HttpContext.Session.GetString("Theme") ?? "dark";

        var gitVersionResult = await _processService.ExecuteAsync(gitPath, new[] { "--version" }, Environment.CurrentDirectory);
        ViewBag.GitVersion = gitVersionResult.Success ? gitVersionResult.StandardOutput.Trim() : "Git not found";

        var settings = new WorkspaceSettings
        {
            WorkingFolder = workingFolder,
            GitExecutable = gitPath,
            Theme = theme,
            RestoreLastRepository = true,
            AutoScanRepositories = true,
            RememberWorkingFolder = true
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

        return Json(new { success = true, settings });
    }
}
