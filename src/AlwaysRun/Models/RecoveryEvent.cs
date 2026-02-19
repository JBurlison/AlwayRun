namespace AlwaysRun.Models;

/// <summary>
/// Represents a single recovery/restart event for a managed application.
/// </summary>
public sealed record RecoveryEvent
{
    /// <summary>
    /// When the recovery event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The type of event.
    /// </summary>
    public required RecoveryEventType EventType { get; init; }

    /// <summary>
    /// The exit code of the process when it crashed (null for non-crash events).
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// How long the process was running before it exited.
    /// </summary>
    public TimeSpan? Uptime { get; init; }

    /// <summary>
    /// The restart attempt number (for backoff tracking).
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// The scheduled delay before the restart was attempted.
    /// </summary>
    public TimeSpan? RestartDelay { get; init; }

    /// <summary>
    /// Optional error message if the restart failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Types of recovery events that can be logged.
/// </summary>
public enum RecoveryEventType
{
    /// <summary>Process started (initial or manual).</summary>
    Started,

    /// <summary>Process exited unexpectedly and a restart was scheduled.</summary>
    Crashed,

    /// <summary>Process was automatically restarted after a crash.</summary>
    Restarted,

    /// <summary>Restart attempt failed.</summary>
    RestartFailed,

    /// <summary>Process was manually stopped.</summary>
    Stopped,

    /// <summary>Process was paused by the user.</summary>
    Paused,

    /// <summary>Process monitoring was resumed by the user.</summary>
    Resumed
}
