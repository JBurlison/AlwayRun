using System.Text.Json.Serialization;

namespace AlwaysRun.Models;

/// <summary>
/// Configuration record for a managed application.
/// Persisted to config.json.
/// </summary>
public record ManagedAppConfig
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public required string FilePath { get; init; }
    public required AppType AppType { get; init; }
    public string? Arguments { get; init; }
    public required string WorkingDirectory { get; init; }
    public bool IsPaused { get; init; }
    public bool UsePowerShellBypass { get; init; }
    
    /// <summary>
    /// Initial delay in seconds before restarting the application after it exits.
    /// Exponential backoff applies to subsequent attempts.
    /// </summary>
    public int RestartDelaySeconds { get; init; } = 2;

    /// <summary>
    /// Periodically restarts this application after the configured number of hours.
    /// The interval is measured from the most recent successful start.
    /// </summary>
    public bool ScheduledRestartEnabled { get; init; }
    public int ScheduledRestartIntervalHours { get; init; } = 24;
    public ScheduledRestartMethod ScheduledRestartMethod { get; init; } = ScheduledRestartMethod.ForceKill;

    /// <summary>
    /// Command executed through cmd.exe for a graceful scheduled restart.
    /// The application has 30 seconds to exit before it is force-killed.
    /// </summary>
    public string? GracefulShutdownCommand { get; init; }
    
    public DateTimeOffset? LastStartTime { get; init; }
    public DateTimeOffset? LastExitTime { get; init; }
    public int? LastExitCode { get; init; }

    /// <summary>
    /// Creates a new ManagedAppConfig with default values for a new application.
    /// </summary>
    public static ManagedAppConfig Create(
        string displayName,
        string filePath,
        AppType appType,
        string workingDirectory,
        string? arguments = null,
        bool usePowerShellBypass = false,
        int restartDelaySeconds = 2) => new()
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            FilePath = filePath,
            AppType = appType,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            IsPaused = false,
            UsePowerShellBypass = usePowerShellBypass,
            RestartDelaySeconds = restartDelaySeconds,
            ScheduledRestartEnabled = false,
            ScheduledRestartIntervalHours = 24,
            ScheduledRestartMethod = ScheduledRestartMethod.ForceKill,
            GracefulShutdownCommand = null,
            LastStartTime = null,
            LastExitTime = null,
            LastExitCode = null
        };
}
