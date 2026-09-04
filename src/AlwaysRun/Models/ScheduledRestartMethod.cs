namespace AlwaysRun.Models;

/// <summary>
/// How a scheduled restart asks the managed application to stop.
/// </summary>
public enum ScheduledRestartMethod
{
    /// <summary>Immediately terminates the managed process tree.</summary>
    ForceKill,

    /// <summary>Runs a user-supplied command, waits for exit, then kills as a fallback.</summary>
    GracefulCommand,

    /// <summary>Sends Ctrl+C to the process console, followed by Y and Enter.</summary>
    CtrlCThenY
}
