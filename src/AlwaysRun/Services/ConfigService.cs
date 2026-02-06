using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// JSON-based configuration service with atomic writes.
/// </summary>
public sealed class ConfigService(ILogger<ConfigService> logger) : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc/>
    public async ValueTask<AppConfiguration> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var filePath = AppPaths.ConfigFilePath;

            if (!File.Exists(filePath))
            {
                logger.LogInformation("Configuration file not found at {FilePath}, creating default configuration", filePath);
                var defaultConfig = AppConfiguration.CreateDefault();
                await SaveInternalAsync(defaultConfig, ct);
                return defaultConfig;
            }

            logger.LogDebug("Loading configuration from {FilePath}", filePath);
            await using var stream = File.OpenRead(filePath);
            var config = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, JsonOptions, ct);

            if (config is null)
            {
                logger.LogWarning("Configuration file was empty or invalid, returning default configuration");
                return AppConfiguration.CreateDefault();
            }

            // Handle schema migration if needed
            if (config.SchemaVersion < AppConfiguration.CurrentSchemaVersion)
            {
                logger.LogInformation("Migrating configuration from schema version {OldVersion} to {NewVersion}",
                    config.SchemaVersion, AppConfiguration.CurrentSchemaVersion);
                config = MigrateConfiguration(config);
                await SaveInternalAsync(config, ct);
            }

            logger.LogInformation("Loaded configuration with {AppCount} managed applications", config.Apps.Count);
            return config;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse configuration file, returning default configuration");
            return AppConfiguration.CreateDefault();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(AppConfiguration config, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await SaveInternalAsync(config, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async ValueTask SaveInternalAsync(AppConfiguration config, CancellationToken ct)
    {
        AppPaths.EnsureDirectoriesExist();

        var filePath = AppPaths.ConfigFilePath;
        var tempPath = filePath + ".tmp";

        logger.LogDebug("Saving configuration to {FilePath} with {AppCount} apps", filePath, config.Apps.Count);

        // Write to temp file first
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, JsonOptions, ct);
        }

        // Atomic move (overwrite)
        File.Move(tempPath, filePath, overwrite: true);

        logger.LogInformation("Configuration saved successfully with {AppCount} managed applications", config.Apps.Count);
    }

    private static AppConfiguration MigrateConfiguration(AppConfiguration config)
    {
        // Future migrations can be added here
        return config with { SchemaVersion = AppConfiguration.CurrentSchemaVersion };
    }
}
