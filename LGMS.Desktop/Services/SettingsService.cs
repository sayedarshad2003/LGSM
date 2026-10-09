using System.IO;
using System.Text.Json;
using LGMS.Models;

namespace LGMS.Desktop.Services;

public interface ISettingsService
{
    WorkspaceSettings LoadSettings();
    void SaveSettings(WorkspaceSettings settings);
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "LGMS");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _settingsFilePath = Path.Combine(dir, "settings.json");
    }

    public WorkspaceSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // fallback to default
        }

        return new WorkspaceSettings
        {
            WorkingFolder = @"e:\Asp.NET Work\Test Project",
            GitExecutable = "git",
            Theme = "dark",
            RememberWorkingFolder = true,
            AutoScanRepositories = true
        };
    }

    public void SaveSettings(WorkspaceSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // ignore save errors
        }
    }
}
