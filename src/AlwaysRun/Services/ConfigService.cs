using System.IO;
using System.Text;
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
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
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
            var json = await File.ReadAllTextAsync(filePath, ct);
            var repairedLegacyJson = false;
            AppConfiguration? config;

            try
            {
                config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
            }
            catch (JsonException firstException)
            {
                // Some early/hand-edited configuration files contain non-breaking
                // spaces and unnecessary escapes such as \_ or \:. They are not
                // strict JSON, but their intent is unambiguous, so repair and retry.
                var repairedJson = RepairLegacyJson(json);
                if (string.Equals(repairedJson, json, StringComparison.Ordinal))
                {
                    throw;
                }

                logger.LogWarning(firstException,
                    "Configuration uses legacy non-standard JSON escaping; repairing it");
                config = JsonSerializer.Deserialize<AppConfiguration>(repairedJson, JsonOptions);
                repairedLegacyJson = true;
            }

            if (config is null)
            {
                logger.LogWarning("Configuration file was empty or invalid, returning default configuration");
                return AppConfiguration.CreateDefault();
            }

            // Handle schema migration if needed
            if (config.SchemaVersion < AppConfiguration.CurrentSchemaVersion || repairedLegacyJson)
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
        var migratedApps = config.Apps.Select(app => app with
        {
            ScheduledRestartIntervalHours = app.ScheduledRestartIntervalHours <= 0
                ? 24
                : app.ScheduledRestartIntervalHours
        }).ToList();

        return config with
        {
            SchemaVersion = AppConfiguration.CurrentSchemaVersion,
            Apps = migratedApps
        };
    }

    /// <summary>
    /// Repairs JSON produced or edited with legacy escaping rules. Only invalid
    /// backslash escapes inside strings are changed; valid JSON escapes remain
    /// byte-for-byte equivalent. Non-breaking spaces outside strings become spaces.
    /// </summary>
    private static string RepairLegacyJson(string json)
    {
        var repaired = new StringBuilder(json.Length);
        var insideString = false;

        for (var i = 0; i < json.Length; i++)
        {
            var current = json[i];

            if (!insideString)
            {
                if (current == '\u00A0')
                {
                    repaired.Append(' ');
                }
                else
                {
                    repaired.Append(current);
                    if (current == '"')
                    {
                        insideString = true;
                    }
                }
                continue;
            }

            if (current == '"')
            {
                repaired.Append(current);
                insideString = false;
                continue;
            }

            if (current != '\\' || i + 1 >= json.Length)
            {
                repaired.Append(current);
                continue;
            }

            var escaped = json[i + 1];
            if (escaped is '"' or '\\' or '/' or 'b' or 'f' or 'n' or 'r' or 't' or 'u')
            {
                repaired.Append(current);
                repaired.Append(escaped);
                i++;
                continue;
            }

            // Drop only the invalid escape marker. The following character is
            // retained on the next iteration (for example, \_ becomes _).
        }

        return repaired.ToString();
    }
}
