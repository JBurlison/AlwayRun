using System.Diagnostics;
using System.IO;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// Launches processes based on application type (exe, ps1, bat/cmd).
/// </summary>
public sealed class ProcessLauncher(ILogger<ProcessLauncher> logger) : IProcessLauncher
{
    /// <inheritdoc/>
    public Result<Process> Start(ManagedAppConfig app)
    {
        if (!File.Exists(app.FilePath))
        {
            logger.LogError("File not found: {FilePath} for app {DisplayName} ({AppId})",
                app.FilePath, app.DisplayName, app.Id);
            return Result<Process>.Failure($"File not found: {app.FilePath}");
        }

        try
        {
            var startInfo = BuildStartInfo(app);

            logger.LogInformation(
                "Starting process for {DisplayName} ({AppId}): {FileName} {Arguments} in {WorkingDirectory}",
                app.DisplayName, app.Id, startInfo.FileName, startInfo.Arguments, startInfo.WorkingDirectory);

            var process = new Process { StartInfo = startInfo };
            process.EnableRaisingEvents = true;

            if (!process.Start())
            {
                logger.LogError("Failed to start process for {DisplayName} ({AppId})", app.DisplayName, app.Id);
                return Result<Process>.Failure("Process.Start returned false.");
            }

            logger.LogInformation(
                "Process started for {DisplayName} ({AppId}) with PID {ProcessId}",
                app.DisplayName, app.Id, process.Id);

            return Result<Process>.Success(process);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception starting process for {DisplayName} ({AppId}): {FilePath}",
                app.DisplayName, app.Id, app.FilePath);
            return Result<Process>.Failure($"Failed to start process: {ex.Message}");
        }
    }

    private static ProcessStartInfo BuildStartInfo(ManagedAppConfig app)
    {
        var workingDirectory = app.WorkingDirectory;
        if (string.IsNullOrEmpty(workingDirectory))
        {
            workingDirectory = Path.GetDirectoryName(app.FilePath) ?? Environment.CurrentDirectory;
        }

        return app.AppType switch
        {
            AppType.Exe => new ProcessStartInfo
            {
                FileName = app.FilePath,
                Arguments = app.Arguments ?? string.Empty,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            },

            AppType.PowerShell => BuildPowerShellStartInfo(app, workingDirectory),

            AppType.Batch => new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = BuildBatchArguments(app),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            },

            _ => throw new ArgumentOutOfRangeException(nameof(app), $"Unknown AppType: {app.AppType}")
        };
    }

    private static ProcessStartInfo BuildPowerShellStartInfo(ManagedAppConfig app, string workingDirectory)
    {
        var arguments = app.UsePowerShellBypass
            ? $"-NoProfile -ExecutionPolicy Bypass -File \"{app.FilePath}\""
            : $"-NoProfile -File \"{app.FilePath}\"";

        if (!string.IsNullOrEmpty(app.Arguments))
        {
            arguments += $" {app.Arguments}";
        }

        return new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
    }

    private static string BuildBatchArguments(ManagedAppConfig app)
    {
        var scriptPath = app.FilePath.Contains(' ')
            ? $"\"{app.FilePath}\""
            : app.FilePath;

        return string.IsNullOrEmpty(app.Arguments)
            ? $"/c {scriptPath}"
            : $"/c {scriptPath} {app.Arguments}";
    }
}
