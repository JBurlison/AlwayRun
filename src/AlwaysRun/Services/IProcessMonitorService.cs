using AlwaysRun.Models;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for managing monitored applications and restart scheduling.
/// </summary>
public interface IProcessMonitorService
{
    /// <summary>
    /// Event raised when an application's status changes.
    /// </summary>
    event EventHandler<AppStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Initializes monitoring with the given applications.
    /// </summary>
    Task InitializeAsync(IEnumerable<ManagedAppConfig> apps, CancellationToken ct = default);

    /// <summary>
    /// Updates the configuration for a specific application.
    /// </summary>
    Task UpdateAppAsync(ManagedAppConfig app, CancellationToken ct = default);

    /// <summary>
    /// Removes an application from monitoring.
    /// </summary>
    Task RemoveAppAsync(Guid appId, CancellationToken ct = default);

    /// <summary>
    /// Starts all non-paused applications.
    /// </summary>
    Task StartAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Stops all running applications.
    /// </summary>
    Task StopAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts a specific application.
    /// </summary>
    Task StartAsync(Guid appId, CancellationToken ct = default);

    /// <summary>
    /// Stops a specific application.
    /// </summary>
    Task StopAsync(Guid appId, CancellationToken ct = default);

    /// <summary>
    /// Pauses monitoring for a specific application.
    /// </summary>
    Task PauseAsync(Guid appId, CancellationToken ct = default);

    /// <summary>
    /// Resumes monitoring for a specific application.
    /// </summary>
    Task ResumeAsync(Guid appId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current status of a specific application.
    /// </summary>
    AppStatus GetStatus(Guid appId);
}
