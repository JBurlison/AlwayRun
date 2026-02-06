using System.IO;
using AlwaysRun.Infrastructure;
using AlwaysRun.Services;
using AlwaysRun.ViewModels;
using AlwaysRun.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using WpfApplication = System.Windows.Application;
using WpfExitEventArgs = System.Windows.ExitEventArgs;
using WpfStartupEventArgs = System.Windows.StartupEventArgs;

namespace AlwaysRun;

/// <summary>
/// Application entry point with DI setup, single-instance enforcement, and tray integration.
/// </summary>
public partial class App : WpfApplication
{
    private const string MutexName = "AlwaysRun_SingleInstance_Mutex";

    private Mutex? _instanceMutex;
    private IServiceProvider? _serviceProvider;
    private ITrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(WpfStartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure directories exist
        AppPaths.EnsureDirectoriesExist();

        // Configure Serilog
        ConfigureSerilog();

        // Check for single instance
        if (!AcquireSingleInstanceMutex())
        {
            Log.Warning("Another instance of AlwaysRun is already running");
            BringExistingInstanceToForeground();
            Shutdown();
            return;
        }

        // Setup DI
        _serviceProvider = ConfigureServices();

        // Initialize tray service
        _trayService = _serviceProvider.GetRequiredService<ITrayService>();
        _trayService.Initialize();
        _trayService.Show();
        _trayService.OpenRequested += OnTrayOpenRequested;
        _trayService.ExitRequested += OnTrayExitRequested;
        _trayService.PauseAllRequested += OnTrayPauseAllRequested;
        _trayService.ResumeAllRequested += OnTrayResumeAllRequested;

        // Create and show main window
        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.Show();

        Log.Information("AlwaysRun application started");
    }

    protected override void OnExit(WpfExitEventArgs e)
    {
        Log.Information("AlwaysRun application exiting");

        _trayService?.Hide();
        _trayService?.Dispose();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        Log.CloseAndFlush();

        base.OnExit(e);
    }

    private static void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: AppPaths.LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Serilog configured, logging to {LogPath}", AppPaths.LogsDirectory);
    }

    private bool AcquireSingleInstanceMutex()
    {
        _instanceMutex = new Mutex(true, MutexName, out var createdNew);
        return createdNew;
    }

    private static void BringExistingInstanceToForeground()
    {
        // Send a message to the existing instance to bring it to foreground
        // For simplicity, we just exit here - the existing instance handles its own tray
        Log.Information("Exiting to allow existing instance to continue");
    }

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        // Services (singletons)
        services.AddSingleton<BackoffPolicy>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IAutoStartService, AutoStartService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<ITrayService, TrayService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<AddEditAppViewModel>();

        // Factory for AddEditAppViewModel
        services.AddSingleton<Func<AddEditAppViewModel>>(sp =>
            () => sp.GetRequiredService<AddEditAppViewModel>());

        // Views
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void OnTrayOpenRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _mainWindow?.BringToForeground();
        });
    }

    private async void OnTrayExitRequested(object? sender, EventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            Log.Information("Exit requested from tray");

            if (_mainWindow != null)
            {
                await _mainWindow.ForceCloseAsync();
            }

            Shutdown();
        });
    }

    private async void OnTrayPauseAllRequested(object? sender, EventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            Log.Information("Pause All requested from tray");
            var mainVm = _serviceProvider?.GetService<MainViewModel>();
            if (mainVm != null)
            {
                await mainVm.PauseAllCommand.ExecuteAsync(null);
            }
        });
    }

    private async void OnTrayResumeAllRequested(object? sender, EventArgs e)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            Log.Information("Resume All requested from tray");
            var mainVm = _serviceProvider?.GetService<MainViewModel>();
            if (mainVm != null)
            {
                await mainVm.ResumeAllCommand.ExecuteAsync(null);
            }
        });
    }
}
