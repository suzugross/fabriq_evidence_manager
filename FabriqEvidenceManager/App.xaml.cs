using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Services.Interfaces;
using FabriqEvidenceManager.ViewModels;
using FabriqEvidenceManager.Views;

namespace FabriqEvidenceManager;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IEvidenceDiscoveryService, EvidenceDiscoveryService>();
        services.AddSingleton<IEvidenceParserService, EvidenceParserService>();
        services.AddTransient<IEvidenceCollectorService, EvidenceCollectorService>();
        services.AddTransient<IExcelExportService, ExcelExportService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
