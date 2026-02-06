namespace AlwaysRun.Models;

/// <summary>
/// Type of managed application.
/// </summary>
public enum AppType
{
    /// <summary>
    /// Executable file (.exe).
    /// </summary>
    Exe,

    /// <summary>
    /// PowerShell script (.ps1).
    /// </summary>
    PowerShell,

    /// <summary>
    /// Batch file (.bat or .cmd).
    /// </summary>
    Batch
}
