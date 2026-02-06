using AlwaysRun.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace AlwaysRun.ViewModels;

/// <summary>
/// View model for a managed application in the list.
/// </summary>
public sealed partial class ManagedAppViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private AppType _appType;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _usePowerShellBypass;

    [ObservableProperty]
    private int _restartDelaySeconds = 2;

    [ObservableProperty]
    private AppStatus _status = AppStatus.Stopped;

    [ObservableProperty]
    private DateTimeOffset? _lastStartTime;

    [ObservableProperty]
    private DateTimeOffset? _lastExitTime;

    [ObservableProperty]
    private int? _lastExitCode;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Gets the formatted last start time for display.
    /// </summary>
    public string LastStartTimeDisplay => 
        LastStartTime?.ToLocalTime().ToString("g") ?? "Never";

    /// <summary>
    /// Gets the formatted last exit time for display.
    /// </summary>
    public string LastExitTimeDisplay => 
        LastExitTime?.ToLocalTime().ToString("g") ?? "Never";

    /// <summary>
    /// Gets the last exit code for display.
    /// </summary>
    public string LastExitCodeDisplay => 
        LastExitCode?.ToString() ?? "-";

    /// <summary>
    /// Gets the status text for display.
    /// </summary>
    public string StatusText => Status switch
    {
        AppStatus.Running => "Running",
        AppStatus.Stopped => "Stopped",
        AppStatus.Paused => "Paused",
        AppStatus.Starting => "Starting...",
        AppStatus.Error => $"Error: {ErrorMessage ?? "Unknown"}",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the status color brush for display.
    /// </summary>
    public WpfBrush StatusBrush => Status switch
    {
        AppStatus.Running => WpfBrushes.Green,
        AppStatus.Stopped => WpfBrushes.Gray,
        AppStatus.Paused => WpfBrushes.Orange,
        AppStatus.Starting => WpfBrushes.DodgerBlue,
        AppStatus.Error => WpfBrushes.Red,
        _ => WpfBrushes.Gray
    };

    /// <summary>
    /// Gets whether the app can be started.
    /// </summary>
    public bool CanStart => Status is AppStatus.Stopped or AppStatus.Error;

    /// <summary>
    /// Gets whether the app can be stopped.
    /// </summary>
    public bool CanStop => Status is AppStatus.Running or AppStatus.Starting;

    /// <summary>
    /// Gets whether the app can be paused.
    /// </summary>
    public bool CanPause => Status is not AppStatus.Paused;

    /// <summary>
    /// Gets whether the app can be resumed.
    /// </summary>
    public bool CanResume => Status is AppStatus.Paused;

    partial void OnStatusChanged(AppStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
    }

    partial void OnLastStartTimeChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(LastStartTimeDisplay));
    }

    partial void OnLastExitTimeChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(LastExitTimeDisplay));
    }

    partial void OnLastExitCodeChanged(int? value)
    {
        OnPropertyChanged(nameof(LastExitCodeDisplay));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    /// Creates a view model from a configuration.
    /// </summary>
    public static ManagedAppViewModel FromConfig(ManagedAppConfig config) => new()
    {
        Id = config.Id,
        DisplayName = config.DisplayName,
        FilePath = config.FilePath,
        Arguments = config.Arguments,
        WorkingDirectory = config.WorkingDirectory,
        AppType = config.AppType,
        IsPaused = config.IsPaused,
        UsePowerShellBypass = config.UsePowerShellBypass,
        RestartDelaySeconds = config.RestartDelaySeconds,
        Status = config.IsPaused ? AppStatus.Paused : AppStatus.Stopped,
        LastStartTime = config.LastStartTime,
        LastExitTime = config.LastExitTime,
        LastExitCode = config.LastExitCode
    };

    /// <summary>
    /// Converts this view model to a configuration record.
    /// </summary>
    public ManagedAppConfig ToConfig() => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        FilePath = FilePath,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        AppType = AppType,
        IsPaused = IsPaused,
        UsePowerShellBypass = UsePowerShellBypass,
        RestartDelaySeconds = RestartDelaySeconds,
        LastStartTime = LastStartTime,
        LastExitTime = LastExitTime,
        LastExitCode = LastExitCode
    };
}
