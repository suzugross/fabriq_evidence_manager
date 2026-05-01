using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using FabriqEvidenceManager.Services;
using FabriqEvidenceManager.Services.Baseline;
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
        services.AddSingleton<NestedEvidenceDiscoveryService>();
        services.AddSingleton<IManifestReaderService, ManifestReaderService>();
        services.AddSingleton<IWindowsLicenseParserService, WindowsLicenseParserService>();
        services.AddSingleton<IOfficeLicenseParserService, OfficeLicenseParserService>();
        services.AddSingleton<ISecurityBaselineParserService, SecurityBaselineParserService>();
        services.AddSingleton<ICertificatesParserService, CertificatesParserService>();
        services.AddSingleton<IBatteryReportParserService, BatteryReportParserService>();
        services.AddSingleton<IHardwareIdentifiersParserService, HardwareIdentifiersParserService>();
        services.AddSingleton<IMemoryInventoryParserService, MemoryInventoryParserService>();
        services.AddSingleton<IEnvironmentVariablesParserService, EnvironmentVariablesParserService>();
        services.AddSingleton<IStartupItemsParserService, StartupItemsParserService>();
        services.AddSingleton<IPnpDevicesParserService, PnpDevicesParserService>();
        services.AddSingleton<IEvidenceParserService, EvidenceParserService>();
        services.AddSingleton<IHostlistService, HostlistService>();
        services.AddSingleton<IEvidenceVerificationService, EvidenceVerificationService>();
        services.AddSingleton<IChecklistParserService, ChecklistParserService>();
        // Baseline plugin comparators (Phase 1 リファクタ)。
        // 新カテゴリを追加する場合は IBaselineComparator 実装を作成し本ブロックに 1 行追加するのみ。
        services.AddSingleton<IBaselineComparator, ExecutionSummaryComparator>();
        services.AddSingleton<IBaselineComparator, SystemInfoComparator>();
        services.AddSingleton<IBaselineComparator, ChecklistComparator>();
        services.AddSingleton<IBaselineComparator, InstalledAppsComparator>();
        services.AddSingleton<IBaselineComparator, LicenseComparator>();
        services.AddSingleton<IBaselineComparator, DomainStatusComparator>();
        services.AddSingleton<IPcMemoService, PcMemoService>();
        services.AddSingleton<IBaselineService, BaselineService>();
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
