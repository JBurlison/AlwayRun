namespace AlwaysRun.Models;

/// <summary>
/// Current status of a managed application.
/// </summary>
public enum AppStatus
{
    /// <summary>
    /// The application is not running.
    /// </summary>
    Stopped,

    /// <summary>
    /// The application is starting.
    /// </summary>
    Starting,

    /// <summary>
    /// The application is running normally.
    /// </summary>
    Running,

    /// <summary>
    /// Monitoring is paused for this application.
    /// </summary>
    Paused,

    /// <summary>
    /// The application encountered an error (failed to start, missing file, etc.).
    /// </summary>
    Error
}
