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
    public CancellationTokenSource? ScheduledRestartCts { get; set; }
    public Task? ScheduledRestartTask { get; set; }
    public bool IsRemoved { get; set; }

    public void Dispose()
    {
        RestartCts?.Cancel();
        RestartCts?.Dispose();
        RestartCts = null;

        ScheduledRestartCts?.Cancel();
        ScheduledRestartCts?.Dispose();
        ScheduledRestartCts = null;

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
    IRecoveryLogService recoveryLog,
    BackoffPolicy backoffPolicy,
    ILogger<ProcessMonitorService> logger) : IProcessMonitorService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, MonitoredAppState> _apps = new();
    private readonly object _lock = new();
    private bool _disposed;
    private volatile bool _autoRestartPaused;

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
            if (state.Status == AppStatus.Running)
            {
                SchedulePeriodicRestart(state);
            }
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
            state.IsRemoved = true;
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
    public async Task SetAutoRestartPausedAsync(bool paused, CancellationToken ct = default)
    {
        _autoRestartPaused = paused;
        logger.LogInformation("Automatic restarts {State}", paused ? "paused" : "resumed");

        if (paused)
        {
            foreach (var state in _apps.Values)
            {
                state.RestartCts?.Cancel();
                state.ScheduledRestartCts?.Cancel();
            }
            return;
        }

        foreach (var state in _apps.Values.Where(s => s.Status == AppStatus.Running))
        {
            SchedulePeriodicRestart(state);
        }

        await StartAllAsync(ct);
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
        state.Config = state.Config with { IsPaused = true };

        // Cancel any pending restart
        state.RestartCts?.Cancel();
        state.ScheduledRestartCts?.Cancel();

        // Stop the process if running
        await StopInternalAsync(state);

        state.Status = AppStatus.Paused;
        RaiseStatusChanged(state);

        _ = recoveryLog.LogEventAsync(appId, new RecoveryEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = RecoveryEventType.Paused
        });
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
        state.Config = state.Config with { IsPaused = false };
        state.AttemptCount = 0; // Reset backoff on manual resume

        _ = recoveryLog.LogEventAsync(appId, new RecoveryEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = RecoveryEventType.Resumed
        });

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

            // Log failed restart attempt
            _ = recoveryLog.LogEventAsync(state.Config.Id, new RecoveryEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = RecoveryEventType.RestartFailed,
                AttemptNumber = state.AttemptCount,
                Error = result.Error
            });

            // Schedule restart with backoff
            ScheduleRestart(state);
            return Task.CompletedTask;
        }

        state.Process = result.Value;
        state.Status = AppStatus.Running;
        state.LastStartTime = DateTimeOffset.UtcNow;
        state.LastError = null;
        RaiseStatusChanged(state);

        // Log recovery event — differentiate initial start from restart
        var eventType = state.AttemptCount > 0 ? RecoveryEventType.Restarted : RecoveryEventType.Started;
        _ = recoveryLog.LogEventAsync(state.Config.Id, new RecoveryEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = eventType,
            AttemptNumber = state.AttemptCount
        });

        // Attach exit handler — do not forward the caller's CancellationToken
        // into the exit handler. Restart lifecycle is managed independently via
        // state.RestartCts, which is cancelled on Stop/Pause/Dispose.
        if (state.Process is not null)
        {
            var startedProcess = state.Process;
            startedProcess.Exited += (_, _) => OnProcessExited(state, startedProcess);
        }

        SchedulePeriodicRestart(state);

        return Task.CompletedTask;
    }

    private void OnProcessExited(MonitoredAppState state, Process exitedProcess)
    {
        try
        {
            // Manual stop/removal clears state.Process before terminating it. A
            // late Exited callback for that process must not schedule a restart.
            if (!ReferenceEquals(state.Process, exitedProcess) || state.IsRemoved)
            {
                return;
            }

            var exitCode = exitedProcess.ExitCode;
            var exitTime = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Process exited for {DisplayName} ({AppId}) with code {ExitCode}",
                state.Config.DisplayName, state.Config.Id, exitCode);

            state.LastExitTime = exitTime;
            state.LastExitCode = exitCode;
            exitedProcess.Dispose();
            state.Process = null;
            state.ScheduledRestartCts?.Cancel();

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

            // Log crash event
            var uptime = state.LastStartTime.HasValue
                ? exitTime - state.LastStartTime.Value
                : (TimeSpan?)null;

            _ = recoveryLog.LogEventAsync(state.Config.Id, new RecoveryEvent
            {
                Timestamp = exitTime,
                EventType = RecoveryEventType.Crashed,
                ExitCode = exitCode,
                Uptime = uptime,
                AttemptNumber = state.AttemptCount
            });

            // Schedule restart
            ScheduleRestart(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling process exit for {DisplayName} ({AppId})",
                state.Config.DisplayName, state.Config.Id);
        }
    }

    private void ScheduleRestart(MonitoredAppState state)
    {
        // Don't restart if paused
        if (_autoRestartPaused || state.IsRemoved || state.Config.IsPaused || state.Status == AppStatus.Paused)
        {
            return;
        }

        // Cancel any existing restart task
        state.RestartCts?.Cancel();
        state.RestartCts?.Dispose();
        state.RestartCts = new CancellationTokenSource();

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

                if (!state.RestartCts.Token.IsCancellationRequested && !_autoRestartPaused && !state.IsRemoved)
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
        state.ScheduledRestartCts?.Cancel();
        state.ScheduledRestartCts?.Dispose();
        state.ScheduledRestartCts = null;

        if (state.Process is null)
        {
            if (state.Status != AppStatus.Paused)
            {
                state.Status = AppStatus.Stopped;
                RaiseStatusChanged(state);
            }
            return Task.CompletedTask;
        }

        if (state.Process.HasExited)
        {
            var exitedProcess = state.Process;
            state.Process = null;
            exitedProcess.Dispose();
            if (state.Status != AppStatus.Paused)
            {
                state.Status = AppStatus.Stopped;
                RaiseStatusChanged(state);
            }
            return Task.CompletedTask;
        }

        // Mark this process as no longer tracked before terminating it. This makes
        // the Exited callback a no-op instead of an automatic restart request.
        var process = state.Process;
        state.Process = null;

        try
        {
            logger.LogInformation("Stopping process for {DisplayName} ({AppId}) with PID {ProcessId}",
                state.Config.DisplayName, state.Config.Id, process.Id);

            process.Kill(entireProcessTree: true);
            process.Dispose();
            state.LastExitTime = DateTimeOffset.UtcNow;

            _ = recoveryLog.LogEventAsync(state.Config.Id, new RecoveryEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = RecoveryEventType.Stopped
            });

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

    private void SchedulePeriodicRestart(MonitoredAppState state)
    {
        state.ScheduledRestartCts?.Cancel();
        state.ScheduledRestartCts?.Dispose();
        state.ScheduledRestartCts = null;

        if (_autoRestartPaused || state.IsRemoved || state.Config.IsPaused ||
            !state.Config.ScheduledRestartEnabled || state.Status != AppStatus.Running)
        {
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(state.Config.ScheduledRestartIntervalHours, 1, 720));
        var cts = new CancellationTokenSource();
        state.ScheduledRestartCts = cts;
        logger.LogInformation("Scheduled periodic restart for {DisplayName} ({AppId}) in {Interval}",
            state.Config.DisplayName, state.Config.Id, interval);

        state.ScheduledRestartTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(interval, cts.Token);
                if (!cts.IsCancellationRequested && !_autoRestartPaused && !state.IsRemoved)
                {
                    // Once a due restart begins, finish the stop/start transition as
                    // one operation. The pause flag is checked again before restart.
                    await PerformScheduledRestartAsync(state, CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Periodic restart cancelled for {DisplayName} ({AppId})",
                    state.Config.DisplayName, state.Config.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Periodic restart failed for {DisplayName} ({AppId})",
                    state.Config.DisplayName, state.Config.Id);
                ScheduleRestart(state);
            }
        }, cts.Token);
    }

    private async Task PerformScheduledRestartAsync(MonitoredAppState state, CancellationToken ct)
    {
        var process = state.Process;
        if (process is null || process.HasExited || _autoRestartPaused ||
            state.Config.IsPaused || state.IsRemoved)
        {
            return;
        }

        logger.LogInformation("Performing scheduled restart for {DisplayName} ({AppId}) using {Method}",
            state.Config.DisplayName, state.Config.Id, state.Config.ScheduledRestartMethod);

        _ = recoveryLog.LogEventAsync(state.Config.Id, new RecoveryEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = RecoveryEventType.ScheduledRestart
        });

        // Make the old process stale before requesting exit. Its exit callback can
        // no longer race the explicit restart below.
        state.Process = null;
        state.Status = AppStatus.Stopped;
        RaiseStatusChanged(state);

        try
        {
            if (state.Config.ScheduledRestartMethod == ScheduledRestartMethod.CtrlCThenY)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                try
                {
                    await ConsoleControlService.SendCtrlCThenYAsync(process, logger, timeout.Token);
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning("Ctrl+C/Y shutdown timed out for {DisplayName}; force-killing it",
                        state.Config.DisplayName);
                }
            }

            if (state.Config.ScheduledRestartMethod == ScheduledRestartMethod.GracefulCommand &&
                !string.IsNullOrWhiteSpace(state.Config.GracefulShutdownCommand))
            {
                Process? command = null;
                try
                {
                    var commandStartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        WorkingDirectory = state.Config.WorkingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    commandStartInfo.ArgumentList.Add("/d");
                    commandStartInfo.ArgumentList.Add("/s");
                    commandStartInfo.ArgumentList.Add("/c");
                    commandStartInfo.ArgumentList.Add(state.Config.GracefulShutdownCommand);
                    command = Process.Start(commandStartInfo);

                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    if (command is not null)
                    {
                        await command.WaitForExitAsync(timeout.Token);
                    }
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning("Graceful shutdown timed out for {DisplayName}; force-killing it",
                        state.Config.DisplayName);
                    if (command is { HasExited: false })
                    {
                        command.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Graceful shutdown command failed for {DisplayName}; force-killing it",
                        state.Config.DisplayName);
                }
                finally
                {
                    command?.Dispose();
                }
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not stop {DisplayName} for its scheduled restart",
                state.Config.DisplayName);

            if (!process.HasExited)
            {
                state.Process = process;
                state.Status = AppStatus.Running;
                RaiseStatusChanged(state);
                SchedulePeriodicRestart(state);
                return;
            }
        }

        state.LastExitTime = DateTimeOffset.UtcNow;
        state.LastExitCode = process.ExitCode;
        process.Dispose();

        if (!_autoRestartPaused && !state.IsRemoved && !state.Config.IsPaused)
        {
            state.AttemptCount = 0;
            await StartInternalAsync(state, ct);
        }
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
