using AlwaysRun.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AlwaysRun.Services;

/// <summary>
/// Manages auto-start via HKCU Registry Run key.
/// </summary>
public sealed class AutoStartService(ILogger<AutoStartService> logger) : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AlwaysRun";

    /// <inheritdoc/>
    public ValueTask<Result> SetEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                logger.LogError("Failed to open registry key {KeyPath}", RunKeyPath);
                return ValueTask.FromResult(Result.Failure("Failed to access registry Run key."));
            }

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    logger.LogError("Could not determine application executable path");
                    return ValueTask.FromResult(Result.Failure("Could not determine application path."));
                }

                key.SetValue(AppName, $"\"{exePath}\"");
                logger.LogInformation("Auto-start enabled. Registered at {KeyPath}\\{AppName} = {ExePath}", RunKeyPath, AppName, exePath);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                logger.LogInformation("Auto-start disabled. Removed {KeyPath}\\{AppName}", RunKeyPath, AppName);
            }

            return ValueTask.FromResult(Result.Success());
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied when modifying registry Run key");
            return ValueTask.FromResult(Result.Failure("Access denied when modifying auto-start setting."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error when modifying registry Run key");
            return ValueTask.FromResult(Result.Failure($"Unexpected error: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (key is null)
            {
                logger.LogWarning("Registry key {KeyPath} not found", RunKeyPath);
                return ValueTask.FromResult(false);
            }

            var value = key.GetValue(AppName);
            var isEnabled = value is not null;
            logger.LogDebug("Auto-start is {Status}", isEnabled ? "enabled" : "disabled");
            return ValueTask.FromResult(isEnabled);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check auto-start status");
            return ValueTask.FromResult(false);
        }
    }
}
