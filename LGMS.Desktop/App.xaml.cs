using System.IO;
using System.Text.Json;
using System.Windows;
using LGMS.Desktop.Services;
using LGMS.Desktop.ViewModels;
using LGMS.Models;
using LGMS.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LGMS.Desktop;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    public IServiceProvider? Services => _serviceProvider;
    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient();

        // 1. Load Configurations from appsettings.json & settings.json
        var (aiOpts, deepSeekOpts, geminiOpts) = LoadAiConfigurations();

        services.AddSingleton(Options.Create(aiOpts));
        services.AddSingleton(Options.Create(deepSeekOpts));
        services.AddSingleton(Options.Create(geminiOpts));

        // 2. Register AI Services
        services.AddSingleton<DeepSeekCommitService>();
        services.AddSingleton<IDeepSeekCommitService>(sp => sp.GetRequiredService<DeepSeekCommitService>());
        services.AddSingleton<IAICommitService>(sp => sp.GetRequiredService<DeepSeekCommitService>());

        services.AddSingleton<GeminiCommitService>();
        services.AddSingleton<IAICommitService>(sp => sp.GetRequiredService<GeminiCommitService>());

        services.AddSingleton<IAICommitManager, AICommitManager>();

        // 3. Register Core Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
        services.AddSingleton<IGitService, GitService>();

        // 4. GitHub Integration Services
        services.AddSingleton<GitAccountOperationLock>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<GitHubCliService>();

        // 5. Voice Assistant
        services.AddSingleton<VoiceAssistantService>();
        services.AddTransient<VoiceAssistantWindow>();

        // 6. Presentation
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    private static (AIOptions AI, DeepSeekOptions DeepSeek, GeminiOptions Gemini) LoadAiConfigurations()
    {
        var ai = new AIOptions { ActiveProvider = "Gemini", ActiveModel = "gemini-3.5-flash-lite" };
        var deepSeek = new DeepSeekOptions { Model = "deepseek-chat" };
        var gemini = new GeminiOptions { Model = "gemini-3.5-flash-lite" };

        try
        {
            // Search for appsettings.json starting from AppDomain.CurrentDomain.BaseDirectory upwards
            var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            string? foundConfigPath = null;

            for (int i = 0; i < 6 && currentDir != null; i++)
            {
                var candidate = Path.Combine(currentDir.FullName, "appsettings.json");
                if (File.Exists(candidate))
                {
                    foundConfigPath = candidate;
                    break;
                }
                currentDir = currentDir.Parent;
            }

            if (foundConfigPath != null)
            {
                var json = File.ReadAllText(foundConfigPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("AI", out var aiEl))
                {
                    if (aiEl.TryGetProperty("ActiveProvider", out var p)) ai.ActiveProvider = p.GetString() ?? ai.ActiveProvider;
                    if (aiEl.TryGetProperty("ActiveModel", out var m)) ai.ActiveModel = m.GetString();
                    if (aiEl.TryGetProperty("MaxDiffLength", out var len)) ai.MaxDiffLength = len.GetInt32();
                }

                if (root.TryGetProperty("DeepSeek", out var dsEl))
                {
                    if (dsEl.TryGetProperty("ApiKey", out var k)) deepSeek.ApiKey = k.GetString() ?? string.Empty;
                    if (dsEl.TryGetProperty("BaseUrl", out var u)) deepSeek.BaseUrl = u.GetString() ?? deepSeek.BaseUrl;
                    if (dsEl.TryGetProperty("Model", out var m)) deepSeek.Model = m.GetString() ?? deepSeek.Model;
                    if (dsEl.TryGetProperty("MaxTokens", out var mt)) deepSeek.MaxTokens = mt.GetInt32();
                    if (dsEl.TryGetProperty("Temperature", out var t)) deepSeek.Temperature = t.GetDouble();
                    if (dsEl.TryGetProperty("MaxDiffLength", out var dl)) deepSeek.MaxDiffLength = dl.GetInt32();
                    if (dsEl.TryGetProperty("TimeoutSeconds", out var to)) deepSeek.TimeoutSeconds = to.GetInt32();
                }

                if (root.TryGetProperty("Gemini", out var gemEl))
                {
                    if (gemEl.TryGetProperty("ApiKey", out var k)) gemini.ApiKey = k.GetString() ?? string.Empty;
                    if (gemEl.TryGetProperty("BaseUrl", out var u)) gemini.BaseUrl = u.GetString() ?? gemini.BaseUrl;
                    if (gemEl.TryGetProperty("Model", out var m)) gemini.Model = m.GetString() ?? gemini.Model;
                    if (gemEl.TryGetProperty("MaxTokens", out var mt)) gemini.MaxTokens = mt.GetInt32();
                    if (gemEl.TryGetProperty("Temperature", out var t)) gemini.Temperature = t.GetDouble();
                    if (gemEl.TryGetProperty("MaxDiffLength", out var dl)) gemini.MaxDiffLength = dl.GetInt32();
                    if (gemEl.TryGetProperty("TimeoutSeconds", out var to)) gemini.TimeoutSeconds = to.GetInt32();
                }
            }

            // Also check local AppData settings.json to see if user set keys there
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localSettingsPath = Path.Combine(appData, "LGMS", "settings.json");
            if (File.Exists(localSettingsPath))
            {
                var localJson = File.ReadAllText(localSettingsPath);
                var localSettings = JsonSerializer.Deserialize<WorkspaceSettings>(localJson);
                if (localSettings != null)
                {
                    if (!string.IsNullOrWhiteSpace(localSettings.ActiveAiProvider))
                        ai.ActiveProvider = localSettings.ActiveAiProvider;
                    if (!string.IsNullOrWhiteSpace(localSettings.SelectedAiModel))
                        ai.ActiveModel = localSettings.SelectedAiModel;
                    if (!string.IsNullOrWhiteSpace(localSettings.GeminiApiKey))
                        gemini.ApiKey = localSettings.GeminiApiKey;
                    if (!string.IsNullOrWhiteSpace(localSettings.DeepSeekApiKey))
                        deepSeek.ApiKey = localSettings.DeepSeekApiKey;
                }
            }
        }
        catch
        {
            // Graceful fallback to defaults
        }

        return (ai, deepSeek, gemini);
    }
}
