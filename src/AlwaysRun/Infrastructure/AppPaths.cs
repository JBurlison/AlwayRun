using System.IO;

namespace AlwaysRun.Infrastructure;

/// <summary>
/// Centralized helper for application data folder paths.
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> _appDataDirectory = new(() =>
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AlwaysRun");
    });

    private static readonly Lazy<string> _configFilePath = new(() =>
        Path.Combine(AppDataDirectory, "config.json"));

    private static readonly Lazy<string> _logsDirectory = new(() =>
        Path.Combine(AppDataDirectory, "logs"));

    private static readonly Lazy<string> _logFilePath = new(() =>
        Path.Combine(LogsDirectory, "alwaysrun-.log"));

    /// <summary>
    /// Gets the application data directory (%APPDATA%/AlwaysRun).
    /// </summary>
    public static string AppDataDirectory => _appDataDirectory.Value;

    /// <summary>
    /// Gets the configuration file path (%APPDATA%/AlwaysRun/config.json).
    /// </summary>
    public static string ConfigFilePath => _configFilePath.Value;

    /// <summary>
    /// Gets the logs directory (%APPDATA%/AlwaysRun/logs).
    /// </summary>
    public static string LogsDirectory => _logsDirectory.Value;

    /// <summary>
    /// Gets the log file path pattern (%APPDATA%/AlwaysRun/logs/alwaysrun-.log).
    /// </summary>
    public static string LogFilePath => _logFilePath.Value;

    /// <summary>
    /// Ensures the application data directory exists.
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
