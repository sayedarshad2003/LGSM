using System.Windows;
using LGMS.Desktop.ViewModels;
using LGMS.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LGMS.Desktop;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

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

        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
        services.AddSingleton<IGitService, GitService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainWindow>();
    }
}
