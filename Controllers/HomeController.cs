using LGMS.Models;
using LGMS.Services;
using LGMS.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LGMS.Controllers;

public class HomeController : Controller
{
    private readonly IRepositoryScanner _scanner;
    private readonly IProcessService _processService;

    public HomeController(IRepositoryScanner scanner, IProcessService processService)
    {
        _scanner = scanner;
        _processService = processService;
    }

    public async Task<IActionResult> Index([FromQuery] string? workspace)
    {
        var workingFolder = workspace ?? HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        var model = new DashboardViewModel
        {
            Settings = new WorkspaceSettings { WorkingFolder = workingFolder },
            IsVsCodeInstalled = await _processService.IsCommandAvailableAsync("code")
        };

        if (Directory.Exists(workingFolder))
        {
            var repos = await _scanner.ScanAsync(workingFolder);
            model.TotalRepositoriesFound = repos.Count;
            model.HasRepositories = repos.Count > 0;
            model.RecentRepositories = repos.Take(5).ToList();
        }

        return View(model);
    }

    [HttpPost]
    public IActionResult SetWorkspace([FromBody] WorkspaceSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.WorkingFolder) || !Directory.Exists(settings.WorkingFolder))
        {
            return BadRequest(new { success = false, message = "Selected directory does not exist." });
        }

        HttpContext.Session.SetString("WorkingFolder", _scanner.NormalizePath(settings.WorkingFolder));
        return Json(new { success = true, folder = settings.WorkingFolder });
    }

    public IActionResult Error()
    {
        return View();
    }
}
