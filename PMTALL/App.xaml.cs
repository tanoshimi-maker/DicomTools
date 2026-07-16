using PMTALL.Services;
using PMTALL.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace PMTALL;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IDicomScanService, DicomScanService>();
        services.AddSingleton<ISortService, SortService>();
        services.AddSingleton<IClassifyService, ClassifyService>();
        services.AddSingleton<IExecutionService, ExecutionService>();
        services.AddSingleton<IDoseFixService, DoseFixService>();
        services.AddSingleton<IRawToDicomService, RawToDicomService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ICtViewerService, CtViewerService>();

        // ViewModels
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<ViewerViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();

        // Show splash (no owner, independent window)
        splash.Show();

        // Allow splash to render before we start heavy work
        await Task.Delay(200);

        // Step 1: Build service provider
        splash.SetStatus("Initializing services...");

        // Give the UI a moment to breathe
        await Task.Delay(150);

        // Step 2: Resolve main window
        splash.SetStatus("Loading DICOM modules...");

        var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
        var mainViewModel = _serviceProvider!.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = mainViewModel;

        // Short delay so the status text is visible
        await Task.Delay(200);

        // Step 3: Finalize
        splash.SetStatus("Starting DICOM Viewer module...");

        await Task.Delay(250);

        // Fade out and close splash
        await splash.FadeOutAndCloseAsync(TimeSpan.FromMilliseconds(400));

        // Show main window
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
