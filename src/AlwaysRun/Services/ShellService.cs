using System.Diagnostics;
using System.IO;
using AlwaysRun.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// Shell operations for Windows Explorer integration.
/// </summary>
public sealed class ShellService(ILogger<ShellService> logger) : IShellService
{
    /// <inheritdoc/>
    public Result OpenFileLocation(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result.Failure("File path cannot be empty.");
        }

        try
        {
            if (File.Exists(filePath))
            {
                // Open Explorer and select the file
                logger.LogDebug("Opening file location: {FilePath}", filePath);
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                return Result.Success();
            }

            // File doesn't exist, try to open the directory
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                logger.LogDebug("File not found, opening directory: {Directory}", directory);
                Process.Start("explorer.exe", $"\"{directory}\"");
                return Result.Success();
            }

            logger.LogWarning("Cannot open location, neither file nor directory exists: {FilePath}", filePath);
            return Result.Failure("The file and its directory do not exist.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open file location: {FilePath}", filePath);
            return Result.Failure($"Failed to open location: {ex.Message}");
        }
    }
}
