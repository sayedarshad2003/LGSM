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
        string workingFolder;
        if (!string.IsNullOrWhiteSpace(workspace) && Directory.Exists(workspace))
        {
            workingFolder = _scanner.NormalizePath(workspace);
            HttpContext.Session.SetString("WorkingFolder", workingFolder);
        }
        else
        {
            workingFolder = HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory;
        }

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

    [HttpPost("Home/SetWorkspace")]
    [HttpPost("api/workspace")]
    public IActionResult SetWorkspace([FromBody] WorkspaceSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings?.WorkingFolder) || !Directory.Exists(settings.WorkingFolder))
        {
            return BadRequest(new { success = false, message = "Selected directory does not exist on disk." });
        }

        var normalized = _scanner.NormalizePath(settings.WorkingFolder);
        HttpContext.Session.SetString("WorkingFolder", normalized);
        return Json(new { success = true, folder = normalized });
    }

    [HttpPost("api/workspace/browse-native")]
    public async Task<IActionResult> BrowseNativeFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            var script = "Add-Type -AssemblyName System.Windows.Forms; $f = New-Object System.Windows.Forms.FolderBrowserDialog; $f.Description = 'Select Git Workspace Directory'; $f.ShowNewFolderButton = $true; if ($f.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $f.SelectedPath }";
            var result = await _processService.ExecuteAsync("powershell", new[] { "-STA", "-NoProfile", "-Command", script }, Environment.CurrentDirectory, timeout: TimeSpan.FromMinutes(2));
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var selectedFolder = result.StandardOutput.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(selectedFolder) && Directory.Exists(selectedFolder))
                {
                    return Json(new { success = true, folder = _scanner.NormalizePath(selectedFolder) });
                }
            }
            return Json(new { success = false, message = "No folder selected or dialog cancelled." });
        }
        else if (OperatingSystem.IsMacOS())
        {
            var script = "choose folder with prompt \"Select Git Workspace Folder\"";
            var result = await _processService.ExecuteAsync("osascript", new[] { "-e", script }, Environment.CurrentDirectory, timeout: TimeSpan.FromMinutes(2));
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Json(new { success = true, folder = result.StandardOutput.Trim() });
            }
            return Json(new { success = false, message = "No folder selected or dialog cancelled." });
        }
        else
        {
            var result = await _processService.ExecuteAsync("zenity", new[] { "--file-selection", "--directory", "--title=Select Git Workspace Folder" }, Environment.CurrentDirectory, timeout: TimeSpan.FromMinutes(2));
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Json(new { success = true, folder = result.StandardOutput.Trim() });
            }
            return Json(new { success = false, message = "No folder selected or dialog cancelled." });
        }
    }

    [HttpGet("api/filesystem/drives")]
    public IActionResult GetDrives()
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new
                {
                    Name = d.Name,
                    VolumeLabel = string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.DriveType.ToString() : d.VolumeLabel,
                    TotalSize = d.TotalSize,
                    FreeSpace = d.AvailableFreeSpace
                })
                .ToList();
            return Json(new { success = true, drives });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("api/filesystem/folders")]
    public IActionResult GetSubfolders([FromQuery] string? path)
    {
        var target = string.IsNullOrWhiteSpace(path) ? (HttpContext.Session.GetString("WorkingFolder") ?? Environment.CurrentDirectory) : path;
        if (!Directory.Exists(target))
        {
            return BadRequest(new { success = false, message = "Directory does not exist." });
        }

        try
        {
            var parent = Directory.GetParent(target)?.FullName;
            var subfolders = Directory.EnumerateDirectories(target)
                .Select(d =>
                {
                    var name = Path.GetFileName(d);
                    var isGit = Directory.Exists(Path.Combine(d, ".git"));
                    return new { Path = _scanner.NormalizePath(d), Name = name, IsGit = isGit };
                })
                .Where(d => !d.Name.StartsWith('.') && !d.Name.StartsWith('$') && !d.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) && !d.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) && !d.Name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name)
                .ToList();

            return Json(new { success = true, current = _scanner.NormalizePath(target), parent, subfolders });
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { success = false, message = "Access denied to folder." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("docs")]
    [HttpGet("landing")]
    public IActionResult Showcase()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
        if (System.IO.File.Exists(filePath))
        {
            return PhysicalFile(filePath, "text/html");
        }
        return Redirect("/docs/index.html");
    }

    public IActionResult Error()
    {
        return View();
    }
}
