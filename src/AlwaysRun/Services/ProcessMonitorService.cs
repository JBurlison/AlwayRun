using System.Collections.Concurrent;
using System.Diagnostics;
using AlwaysRun.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// Internal state for a monitored application.
/// </summary>
internal sealed class MonitoredAppState : IDisposable
{
    public required ManagedAppConfig Config { get; set; }
    public Process? Process { get; set; }
    public AppStatus Status { get; set; } = AppStatus.Stopped;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastStartTime { get; set; }
    public DateTimeOffset? LastExitTime { get; set; }
    public int? LastExitCode { get; set; }
    public string? LastError { get; set; }
    public CancellationTokenSource? RestartCts { get; set; }
    public Task? RestartTask { get; set; }

    public void Dispose()
    {
        RestartCts?.Cancel();
        RestartCts?.Dispose();
        RestartCts = null;

        try
        {
            if (Process is { HasExited: false })
            {
                Process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore kill errors during dispose
        }

        Process?.Dispose();
        Process = null;
    }
}

/// <summary>
/// Manages process monitoring, exit handling, and restart scheduling with exponential backoff.
/// </summary>
public sealed class ProcessMonitorService(
    IProcessLauncher processLauncher,
    BackoffPolicy backoffPolicy,
    ILogger<ProcessMonitorService> logger) : IProcessMonitorService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, MonitoredAppState> _apps = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <inheritdoc/>
    public event EventHandler<AppStatusChangedEventArgs>? StatusChanged;

    /// <inheritdoc/>
    public Task InitializeAsync(IEnumerable<ManagedAppConfig> apps, CancellationToken ct = default)
    {
        foreach (var app in apps)
        {
            var state = new MonitoredAppState
            {
                Config = app,
                Status = app.IsPaused ? AppStatus.Paused : AppStatus.Stopped,
                LastStartTime = app.LastStartTime,
                LastExitTime = app.LastExitTime,
                LastExitCode = app.LastExitCode
            };
            _apps[app.Id] = state;
            logger.LogDebug("Initialized monitoring for {DisplayName} ({AppId}), IsPaused={IsPaused}",
                app.DisplayName, app.Id, app.IsPaused);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateAppAsync(ManagedAppConfig app, CancellationToken ct = default)
    {
        if (_apps.TryGetValue(app.Id, out var state))
        {
            state.Config = app;
            logger.LogInformation("Updated configuration for {DisplayName} ({AppId})", app.DisplayName, app.Id);
        }
        else
        {
            var newState = new MonitoredAppState
            {
                Config = app,
                Status = app.IsPaused ? AppStatus.Paused : AppStatus.Stopped
            };
            _apps[app.Id] = newState;
            logger.LogInformation("Added new app for monitoring: {DisplayName} ({AppId})", app.DisplayName, app.Id);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task RemoveAppAsync(Guid appId, CancellationToken ct = default)
    {
        if (_apps.TryRemove(appId, out var state))
        {
            logger.LogInformation("Removing app from monitoring: {DisplayName} ({AppId})",
                state.Config.DisplayName, appId);
            await StopInternalAsync(state);
            state.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task StartAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Starting all non-paused applications");

        var tasks = _apps.Values
            .Where(s => !s.Config.IsPaused && s.Status != AppStatus.Running && s.Status != AppStatus.Starting)
            .Select(s => StartInternalAsync(s, ct))
            .ToList();

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public async Task StopAllAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Stopping all running applications");

        var tasks = _apps.Values
            .Where(s => s.Status is AppStatus.Running or AppStatus.Starting)
            .Select(StopInternalAsync)
            .ToList();

        await Task.WhenAll(tasks);
    }

    /// <inheritdoc/>
    public Task StartAsync(Guid appId, CancellationToken ct = default)
    {
        if (!_apps.TryGetValue(appId, out var state))
        {
            logger.LogWarning("Cannot start unknown app {AppId}", appId);
            return Task.CompletedTask;
        }

        return StartInternalAsync(state, ct);
    }

    /// <inheritdoc/>
    public Task StopAsync(Guid appId, CancellationToken ct = default)
    {
        if (!_apps.TryGetValue(appId, out var state))
        {
            logger.LogWarning("Cannot stop unknown app {AppId}", appId);
            return Task.CompletedTask;
        }

        return StopInternalAsync(state);
    }

    /// <inheritdoc/>
    public async Task PauseAsync(Guid appId, CancellationToken ct = default)
    {
        if (!_apps.TryGetValue(appId, out var state))
        {
            logger.LogWarning("Cannot pause unknown app {AppId}", appId);
            return;
        }

        logger.LogInformation("Pausing monitoring for {DisplayName} ({AppId})", state.Config.DisplayName, appId);

        // Cancel any pending restart
        state.RestartCts?.Cancel();

        // Stop the process if running
        await StopInternalAsync(state);

        state.Status = AppStatus.Paused;
        RaiseStatusChanged(state);
    }

    /// <inheritdoc/>
    public Task ResumeAsync(Guid appId, CancellationToken ct = default)
    {
        if (!_apps.TryGetValue(appId, out var state))
        {
            logger.LogWarning("Cannot resume unknown app {AppId}", appId);
            return Task.CompletedTask;
        }

        if (state.Status != AppStatus.Paused)
        {
            logger.LogDebug("App {DisplayName} ({AppId}) is not paused, status={Status}",
                state.Config.DisplayName, appId, state.Status);
            return Task.CompletedTask;
        }

        logger.LogInformation("Resuming monitoring for {DisplayName} ({AppId})", state.Config.DisplayName, appId);
        state.AttemptCount = 0; // Reset backoff on manual resume
        return StartInternalAsync(state, ct);
    }

    /// <inheritdoc/>
    public AppStatus GetStatus(Guid appId)
    {
        return _apps.TryGetValue(appId, out var state) ? state.Status : AppStatus.Stopped;
    }

    private Task StartInternalAsync(MonitoredAppState state, CancellationToken ct)
    {
        lock (_lock)
        {
            if (state.Status is AppStatus.Running or AppStatus.Starting)
            {
                return Task.CompletedTask;
            }

            state.Status = AppStatus.Starting;
            state.LastError = null;
            RaiseStatusChanged(state);
        }

        logger.LogInformation("Starting {DisplayName} ({AppId}), attempt {Attempt}",
            state.Config.DisplayName, state.Config.Id, state.AttemptCount);

        var result = processLauncher.Start(state.Config);

        if (!result.IsSuccess)
        {
            logger.LogError("Failed to start {DisplayName} ({AppId}): {Error}",
                state.Config.DisplayName, state.Config.Id, result.Error);

            state.Status = AppStatus.Error;
            state.LastError = result.Error;
            RaiseStatusChanged(state);

            // Schedule restart with backoff
            ScheduleRestart(state, ct);
            return Task.CompletedTask;
        }

        state.Process = result.Value;
        state.Status = AppStatus.Running;
        state.LastStartTime = DateTimeOffset.UtcNow;
        state.LastError = null;
        RaiseStatusChanged(state);

        // Attach exit handler
        if (state.Process is not null)
        {
            state.Process.Exited += (_, _) => OnProcessExited(state, ct);
        }

        return Task.CompletedTask;
    }

    private void OnProcessExited(MonitoredAppState state, CancellationToken ct)
    {
        try
        {
            var exitCode = state.Process?.ExitCode ?? -1;
            var exitTime = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Process exited for {DisplayName} ({AppId}) with code {ExitCode}",
                state.Config.DisplayName, state.Config.Id, exitCode);

            state.LastExitTime = exitTime;
            state.LastExitCode = exitCode;
            state.Process?.Dispose();
            state.Process = null;

            // Check if process ran long enough to reset backoff
            if (state.LastStartTime.HasValue && backoffPolicy.ShouldResetAttempts(state.LastStartTime.Value))
            {
                logger.LogDebug("Process {DisplayName} ({AppId}) ran healthy, resetting backoff",
                    state.Config.DisplayName, state.Config.Id);
                state.AttemptCount = 0;
            }

            // If paused, just update status and don't restart
            if (state.Config.IsPaused || state.Status == AppStatus.Paused)
            {
                state.Status = AppStatus.Paused;
                RaiseStatusChanged(state);
                return;
            }

            state.Status = AppStatus.Stopped;
            RaiseStatusChanged(state);

            // Schedule restart
            ScheduleRestart(state, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling process exit for {DisplayName} ({AppId})",
                state.Config.DisplayName, state.Config.Id);
        }
    }

    private void ScheduleRestart(MonitoredAppState state, CancellationToken ct)
    {
        // Don't restart if paused
        if (state.Config.IsPaused || state.Status == AppStatus.Paused)
        {
            return;
        }

        // Cancel any existing restart task
        state.RestartCts?.Cancel();
        state.RestartCts?.Dispose();
        state.RestartCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Use app's configured restart delay as the base
        var customInitialDelay = TimeSpan.FromSeconds(state.Config.RestartDelaySeconds);
        var delay = backoffPolicy.GetNextDelay(state.AttemptCount, addJitter: true, customInitialDelay);
        state.AttemptCount++;

        logger.LogInformation(
            "Scheduling restart for {DisplayName} ({AppId}) in {Delay:F1}s (attempt {Attempt})",
            state.Config.DisplayName, state.Config.Id, delay.TotalSeconds, state.AttemptCount);

        state.RestartTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, state.RestartCts.Token);

                if (!state.RestartCts.Token.IsCancellationRequested)
                {
                    await StartInternalAsync(state, state.RestartCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Restart cancelled for {DisplayName} ({AppId})",
                    state.Config.DisplayName, state.Config.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during scheduled restart for {DisplayName} ({AppId})",
                    state.Config.DisplayName, state.Config.Id);
            }
        }, state.RestartCts.Token);
    }

    private Task StopInternalAsync(MonitoredAppState state)
    {
        // Cancel pending restart
        state.RestartCts?.Cancel();
        state.RestartCts?.Dispose();
        state.RestartCts = null;

        if (state.Process is null || state.Process.HasExited)
        {
            if (state.Status != AppStatus.Paused)
            {
                state.Status = AppStatus.Stopped;
                RaiseStatusChanged(state);
            }
            return Task.CompletedTask;
        }

        try
        {
            logger.LogInformation("Stopping process for {DisplayName} ({AppId}) with PID {ProcessId}",
                state.Config.DisplayName, state.Config.Id, state.Process.Id);

            state.Process.Kill(entireProcessTree: true);
            state.Process.Dispose();
            state.Process = null;
            state.LastExitTime = DateTimeOffset.UtcNow;

            if (state.Status != AppStatus.Paused)
            {
                state.Status = AppStatus.Stopped;
                RaiseStatusChanged(state);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping process for {DisplayName} ({AppId})",
                state.Config.DisplayName, state.Config.Id);
        }

        return Task.CompletedTask;
    }

    private void RaiseStatusChanged(MonitoredAppState state)
    {
        StatusChanged?.Invoke(this, AppStatusChangedEventArgs.FromStatus(
            state.Config.Id,
            state.Status,
            state.LastStartTime,
            state.LastExitTime,
            state.LastExitCode,
            state.LastError));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var state in _apps.Values)
        {
            state.Dispose();
        }

        _apps.Clear();
    }
}
