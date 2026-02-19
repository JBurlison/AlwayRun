using System.Collections.ObjectModel;
using AlwaysRun.Models;
using AlwaysRun.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlwaysRun.ViewModels;

/// <summary>
/// View model for the recovery history dialog.
/// </summary>
public sealed partial class HistoryViewModel : ViewModelBase
{
    private readonly IRecoveryLogService _recoveryLogService;
    private readonly Guid _appId;

    [ObservableProperty]
    private string _windowTitle = "Recovery History";

    [ObservableProperty]
    private ObservableCollection<RecoveryEventViewModel> _events = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _summary = string.Empty;

    public HistoryViewModel(IRecoveryLogService recoveryLogService, Guid appId, string displayName)
    {
        _recoveryLogService = recoveryLogService;
        _appId = appId;
        WindowTitle = $"Recovery History — {displayName}";
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;

        try
        {
            var history = await _recoveryLogService.GetHistoryAsync(_appId, ct: ct);

            Events.Clear();
            foreach (var entry in history)
            {
                Events.Add(RecoveryEventViewModel.FromEvent(entry));
            }

            var crashCount = history.Count(e => e.EventType == RecoveryEventType.Crashed);
            var restartCount = history.Count(e => e.EventType == RecoveryEventType.Restarted);
            Summary = $"{history.Count} event(s) — {crashCount} crash(es), {restartCount} recovery(ies)";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        await _recoveryLogService.ClearHistoryAsync(_appId);
        Events.Clear();
        Summary = "History cleared";
    }

    /// <summary>
    /// Event raised when the dialog should close.
    /// </summary>
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// View model for a single recovery event row in the history list.
/// </summary>
public sealed partial class RecoveryEventViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _timestamp = string.Empty;

    [ObservableProperty]
    private string _eventType = string.Empty;

    [ObservableProperty]
    private string _exitCode = string.Empty;

    [ObservableProperty]
    private string _uptime = string.Empty;

    [ObservableProperty]
    private string _attempt = string.Empty;

    [ObservableProperty]
    private string _error = string.Empty;

    public static RecoveryEventViewModel FromEvent(RecoveryEvent e) => new()
    {
        Timestamp = e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
        EventType = FormatEventType(e.EventType),
        ExitCode = e.ExitCode?.ToString() ?? "-",
        Uptime = e.Uptime.HasValue ? FormatUptime(e.Uptime.Value) : "-",
        Attempt = e.AttemptNumber > 0 ? $"#{e.AttemptNumber}" : "-",
        Error = e.Error ?? string.Empty
    };

    private static string FormatEventType(RecoveryEventType type) => type switch
    {
        RecoveryEventType.Started => "Started",
        RecoveryEventType.Crashed => "Crashed",
        RecoveryEventType.Restarted => "Restarted",
        RecoveryEventType.RestartFailed => "Restart Failed",
        RecoveryEventType.Stopped => "Stopped",
        RecoveryEventType.Paused => "Paused",
        RecoveryEventType.Resumed => "Resumed",
        _ => type.ToString()
    };

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        if (uptime.TotalMinutes >= 1)
            return $"{uptime.Minutes}m {uptime.Seconds}s";
        return $"{uptime.TotalSeconds:F1}s";
    }
}
