using AlwaysRun.Models;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for loading and saving application configuration.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Loads the application configuration from disk.
    /// Returns default configuration if file doesn't exist.
    /// </summary>
    ValueTask<AppConfiguration> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the application configuration to disk with atomic write.
    /// </summary>
    ValueTask SaveAsync(AppConfiguration config, CancellationToken ct = default);
}
