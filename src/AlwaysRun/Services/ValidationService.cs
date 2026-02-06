using System.IO;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// Validates file types and managed application configurations.
/// </summary>
public sealed class ValidationService(ILogger<ValidationService> logger) : IValidationService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".ps1", ".bat", ".cmd"
    };

    /// <inheritdoc/>
    public Result ValidateManagedApp(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Result.Failure("File path cannot be empty.");
        }

        if (!File.Exists(filePath))
        {
            logger.LogWarning("File not found during validation: {FilePath}", filePath);
            return Result.Failure($"File not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            return Result.Failure("File must have an extension (.exe, .ps1, .bat, or .cmd).");
        }

        if (!SupportedExtensions.Contains(extension))
        {
            logger.LogWarning("Unsupported file extension: {Extension} for {FilePath}", extension, filePath);
            return Result.Failure($"Unsupported file type: {extension}. Supported types are .exe, .ps1, .bat, and .cmd.");
        }

        logger.LogDebug("Validation passed for {FilePath}", filePath);
        return Result.Success();
    }

    /// <inheritdoc/>
    public AppType ResolveAppType(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

        return extension switch
        {
            ".exe" => AppType.Exe,
            ".ps1" => AppType.PowerShell,
            ".bat" or ".cmd" => AppType.Batch,
            _ => AppType.Exe // Default fallback
        };
    }

    /// <inheritdoc/>
    public Result ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure("Display name cannot be empty.");
        }

        if (displayName.Length > 100)
        {
            return Result.Failure("Display name cannot exceed 100 characters.");
        }

        return Result.Success();
    }
}
