using AlwaysRun.Infrastructure;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for shell operations like opening file locations.
/// </summary>
public interface IShellService
{
    /// <summary>
    /// Opens the containing folder for the specified file in Windows Explorer.
    /// </summary>
    Result OpenFileLocation(string filePath);
}
