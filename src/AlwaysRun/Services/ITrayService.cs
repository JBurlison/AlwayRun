namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for system tray icon functionality.
/// </summary>
public interface ITrayService : IDisposable
{
    /// <summary>
    /// Initializes the tray icon.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Shows the tray icon.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the tray icon.
    /// </summary>
    void Hide();

    /// <summary>
    /// Event raised when user requests to open the main window.
    /// </summary>
    event EventHandler? OpenRequested;

    /// <summary>
    /// Event raised when user requests to exit the application.
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Event raised when user requests to pause all applications.
    /// </summary>
    event EventHandler? PauseAllRequested;

    /// <summary>
    /// Event raised when user requests to resume all applications.
    /// </summary>
    event EventHandler? ResumeAllRequested;
}
