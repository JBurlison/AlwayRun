using AlwaysRun.Infrastructure;
using AlwaysRun.Models;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for validating managed application configurations.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates a file path for use as a managed application.
    /// </summary>
    Result ValidateManagedApp(string filePath);

    /// <summary>
    /// Resolves the application type based on file extension.
    /// </summary>
    AppType ResolveAppType(string filePath);

    /// <summary>
    /// Validates a display name.
    /// </summary>
    Result ValidateDisplayName(string? displayName);
}
