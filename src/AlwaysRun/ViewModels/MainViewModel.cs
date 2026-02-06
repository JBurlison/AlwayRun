using System.Collections.ObjectModel;
using AlwaysRun.Models;
using AlwaysRun.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;

namespace AlwaysRun.ViewModels;

/// <summary>
/// Main view model for the application window.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IProcessMonitorService _processMonitor;
    private readonly IAutoStartService _autoStartService;
    private readonly IShellService _shellService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Func<AddEditAppViewModel> _addEditVmFactory;

    private AppConfiguration _currentConfig = AppConfiguration.CreateDefault();

    [ObservableProperty]
    private ObservableCollection<ManagedAppViewModel> _apps = [];

    [ObservableProperty]
    private ManagedAppViewModel? _selectedApp;

    [ObservableProperty]
    private bool _autoStartEnabled;

    [ObservableProperty]
    private bool _exitOnClose;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainViewModel(
        IConfigService configService,
        IProcessMonitorService processMonitor,
        IAutoStartService autoStartService,
        IShellService shellService,
        ILogger<MainViewModel> logger,
        Func<AddEditAppViewModel> addEditVmFactory)
    {
        _configService = configService;
        _processMonitor = processMonitor;
        _autoStartService = autoStartService;
        _shellService = shellService;
        _logger = logger;
        _addEditVmFactory = addEditVmFactory;

        _processMonitor.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Event raised when an add/edit dialog should be shown.
    /// </summary>
    public event EventHandler<AddEditAppViewModel>? ShowAddEditDialogRequested;

    /// <summary>
    /// Initializes the view model and loads configuration.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        StatusMessage = "Loading configuration...";

        try
        {
            _logger.LogInformation("Initializing MainViewModel");

            // Load configuration
            _currentConfig = await _configService.LoadAsync(ct);

            // Populate apps collection
            Apps.Clear();
            foreach (var app in _currentConfig.Apps)
            {
                Apps.Add(ManagedAppViewModel.FromConfig(app));
            }

            // Set auto-start state
            AutoStartEnabled = await _autoStartService.IsEnabledAsync(ct);
            ExitOnClose = _currentConfig.ExitOnClose;

            // Initialize process monitor
            await _processMonitor.InitializeAsync(_currentConfig.Apps, ct);

            // Start all non-paused apps
            await _processMonitor.StartAllAsync(ct);

            StatusMessage = $"Loaded {Apps.Count} application(s)";
            _logger.LogInformation("MainViewModel initialized with {AppCount} apps", Apps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MainViewModel");
            StatusMessage = "Error loading configuration";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        _ = SetAutoStartAsync(value);
    }

    partial void OnExitOnCloseChanged(bool value)
    {
        _ = SaveConfigAsync();
    }

    private async Task SetAutoStartAsync(bool enabled)
    {
        var result = await _autoStartService.SetEnabledAsync(enabled);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to set auto-start: {Error}", result.Error);
            StatusMessage = $"Failed to update auto-start: {result.Error}";
        }
        else
        {
            StatusMessage = enabled ? "Auto-start enabled" : "Auto-start disabled";
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        _logger.LogDebug("Add command invoked");

        var vm = _addEditVmFactory();
        vm.IsEditMode = false;

        ShowAddEditDialogRequested?.Invoke(this, vm);

        if (vm.DialogResult == true && vm.ResultConfig is not null)
        {
            var config = vm.ResultConfig;
            var appVm = ManagedAppViewModel.FromConfig(config);

            Apps.Add(appVm);
            SelectedApp = appVm;

            await _processMonitor.UpdateAppAsync(config);
            await SaveConfigAsync();

            // Start the new app if not paused
            if (!config.IsPaused)
            {
                await _processMonitor.StartAsync(config.Id);
            }

            StatusMessage = $"Added: {config.DisplayName}";
            _logger.LogInformation("Added new app: {DisplayName} ({AppId})", config.DisplayName, config.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task EditAsync()
    {
        if (SelectedApp is null) return;

        _logger.LogDebug("Edit command invoked for {DisplayName}", SelectedApp.DisplayName);

        var vm = _addEditVmFactory();
        vm.IsEditMode = true;
        vm.LoadFromConfig(SelectedApp.ToConfig());

        ShowAddEditDialogRequested?.Invoke(this, vm);

        if (vm.DialogResult == true && vm.ResultConfig is not null)
        {
            var config = vm.ResultConfig;

            // Update the view model
            SelectedApp.DisplayName = config.DisplayName;
            SelectedApp.FilePath = config.FilePath;
            SelectedApp.Arguments = config.Arguments;
            SelectedApp.WorkingDirectory = config.WorkingDirectory;
            SelectedApp.AppType = config.AppType;
            SelectedApp.UsePowerShellBypass = config.UsePowerShellBypass;

            await _processMonitor.UpdateAppAsync(config);
            await SaveConfigAsync();

            StatusMessage = $"Updated: {config.DisplayName}";
            _logger.LogInformation("Updated app: {DisplayName} ({AppId})", config.DisplayName, config.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task RemoveAsync()
    {
        if (SelectedApp is null) return;

        var result = WpfMessageBox.Show(
            $"Are you sure you want to remove '{SelectedApp.DisplayName}'?",
            "Confirm Remove",
            WpfMessageBoxButton.YesNo,
            WpfMessageBoxImage.Question);

        if (result != WpfMessageBoxResult.Yes) return;

        var appId = SelectedApp.Id;
        var displayName = SelectedApp.DisplayName;

        _logger.LogInformation("Removing app: {DisplayName} ({AppId})", displayName, appId);

        await _processMonitor.RemoveAppAsync(appId);
        Apps.Remove(SelectedApp);
        SelectedApp = null;

        await SaveConfigAsync();

        StatusMessage = $"Removed: {displayName}";
    }

    [RelayCommand(CanExecute = nameof(CanPauseSelected))]
    private async Task PauseAsync()
    {
        if (SelectedApp is null) return;

        _logger.LogInformation("Pausing app: {DisplayName} ({AppId})", SelectedApp.DisplayName, SelectedApp.Id);

        SelectedApp.IsPaused = true;
        await _processMonitor.PauseAsync(SelectedApp.Id);
        await SaveConfigAsync();

        StatusMessage = $"Paused: {SelectedApp.DisplayName}";
        NotifyAppCommandsCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanResumeSelected))]
    private async Task ResumeAsync()
    {
        if (SelectedApp is null) return;

        _logger.LogInformation("Resuming app: {DisplayName} ({AppId})", SelectedApp.DisplayName, SelectedApp.Id);

        SelectedApp.IsPaused = false;
        await _processMonitor.ResumeAsync(SelectedApp.Id);
        await SaveConfigAsync();

        StatusMessage = $"Resumed: {SelectedApp.DisplayName}";
        NotifyAppCommandsCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartSelected))]
    private async Task StartAsync()
    {
        if (SelectedApp is null) return;

        _logger.LogInformation("Starting app: {DisplayName} ({AppId})", SelectedApp.DisplayName, SelectedApp.Id);

        await _processMonitor.StartAsync(SelectedApp.Id);

        StatusMessage = $"Starting: {SelectedApp.DisplayName}";
        NotifyAppCommandsCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStopSelected))]
    private async Task StopAsync()
    {
        if (SelectedApp is null) return;

        _logger.LogInformation("Stopping app: {DisplayName} ({AppId})", SelectedApp.DisplayName, SelectedApp.Id);

        await _processMonitor.StopAsync(SelectedApp.Id);

        StatusMessage = $"Stopped: {SelectedApp.DisplayName}";
        NotifyAppCommandsCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private void OpenLocation()
    {
        if (SelectedApp is null) return;

        _logger.LogDebug("Opening location for {DisplayName}", SelectedApp.DisplayName);
        var result = _shellService.OpenFileLocation(SelectedApp.FilePath);

        if (!result.IsSuccess)
        {
            StatusMessage = $"Failed to open location: {result.Error}";
        }
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        _logger.LogInformation("Starting all applications");
        StatusMessage = "Starting all applications...";

        await _processMonitor.StartAllAsync();

        StatusMessage = "All applications started";
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        _logger.LogInformation("Stopping all applications");
        StatusMessage = "Stopping all applications...";

        await _processMonitor.StopAllAsync();

        StatusMessage = "All applications stopped";
    }

    [RelayCommand]
    private async Task PauseAllAsync()
    {
        _logger.LogInformation("Pausing all applications");

        foreach (var app in Apps)
        {
            if (!app.IsPaused)
            {
                app.IsPaused = true;
                await _processMonitor.PauseAsync(app.Id);
            }
        }

        await SaveConfigAsync();
        StatusMessage = "All applications paused";
    }

    [RelayCommand]
    private async Task ResumeAllAsync()
    {
        _logger.LogInformation("Resuming all applications");

        foreach (var app in Apps)
        {
            if (app.IsPaused)
            {
                app.IsPaused = false;
                await _processMonitor.ResumeAsync(app.Id);
            }
        }

        await SaveConfigAsync();
        StatusMessage = "All applications resumed";
    }

    private bool HasSelectedApp() => SelectedApp is not null;
    private bool CanPauseSelected() => SelectedApp is not null && !SelectedApp.IsPaused;
    private bool CanResumeSelected() => SelectedApp is not null && SelectedApp.IsPaused;
    private bool CanStartSelected() => SelectedApp is not null && SelectedApp.CanStart;
    private bool CanStopSelected() => SelectedApp is not null && SelectedApp.CanStop;

    private void NotifyAppCommandsCanExecuteChanged()
    {
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAppChanged(ManagedAppViewModel? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OpenLocationCommand.NotifyCanExecuteChanged();
        NotifyAppCommandsCanExecuteChanged();
    }

    private void OnStatusChanged(object? sender, AppStatusChangedEventArgs e)
    {
        // Marshal to UI thread
        WpfApplication.Current?.Dispatcher.Invoke(() =>
        {
            var app = Apps.FirstOrDefault(a => a.Id == e.AppId);
            if (app is null) return;

            app.Status = e.Status;
            app.LastStartTime = e.LastStartTime ?? app.LastStartTime;
            app.LastExitTime = e.LastExitTime ?? app.LastExitTime;
            app.LastExitCode = e.LastExitCode ?? app.LastExitCode;
            app.ErrorMessage = e.Error;

            _logger.LogDebug("Status updated for {DisplayName}: {Status}", app.DisplayName, e.Status);

            // Update command states when status changes
            if (app == SelectedApp)
            {
                NotifyAppCommandsCanExecuteChanged();
            }
        });
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            var apps = Apps.Select(a => a.ToConfig()).ToList();
            var config = _currentConfig with
            {
                Apps = apps,
                ExitOnClose = ExitOnClose
            };

            await _configService.SaveAsync(config);
            _currentConfig = config;

            _logger.LogDebug("Configuration saved with {AppCount} apps", apps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            StatusMessage = "Failed to save configuration";
        }
    }

    /// <summary>
    /// Cleanup when the application is closing.
    /// </summary>
    public async Task ShutdownAsync()
    {
        _logger.LogInformation("MainViewModel shutting down");
        await _processMonitor.StopAllAsync();
    }
}
