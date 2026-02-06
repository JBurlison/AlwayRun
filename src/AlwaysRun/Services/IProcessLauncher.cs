using System.Diagnostics;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for starting processes.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Starts a process for the given managed application configuration.
    /// </summary>
    Result<Process> Start(ManagedAppConfig app);
}
