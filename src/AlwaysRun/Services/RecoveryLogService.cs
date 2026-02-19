using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlwaysRun.Infrastructure;
using AlwaysRun.Models;
using Microsoft.Extensions.Logging;

namespace AlwaysRun.Services;

/// <summary>
/// JSON-lines file-based recovery log service. One file per application, stored in
/// %APPDATA%/AlwaysRun/history/{appId}.jsonl — one JSON object per line for fast appends.
/// </summary>
public sealed class RecoveryLogService(ILogger<RecoveryLogService> logger) : IRecoveryLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string HistoryDirectory => Path.Combine(AppPaths.AppDataDirectory, "history");

    private static string GetLogFilePath(Guid appId) =>
        Path.Combine(HistoryDirectory, $"{appId}.jsonl");

    /// <inheritdoc/>
    public async ValueTask LogEventAsync(Guid appId, RecoveryEvent recoveryEvent, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(HistoryDirectory);

            var filePath = GetLogFilePath(appId);
            var json = JsonSerializer.Serialize(recoveryEvent, JsonOptions);

            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, ct);

            logger.LogDebug("Logged {EventType} event for app {AppId}", recoveryEvent.EventType, appId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write recovery event for app {AppId}", appId);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RecoveryEvent>> GetHistoryAsync(Guid appId, int maxEntries = 500, CancellationToken ct = default)
    {
        var filePath = GetLogFilePath(appId);

        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, ct);
            var events = new List<RecoveryEvent>(Math.Min(lines.Length, maxEntries));

            // Read all lines, then take the most recent entries
            for (var i = lines.Length - 1; i >= 0 && events.Count < maxEntries; i--)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<RecoveryEvent>(line, JsonOptions);
                    if (entry is not null)
                    {
                        events.Add(entry);
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Skipping malformed history line for app {AppId}", appId);
                }
            }

            return events;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read recovery history for app {AppId}", appId);
            return [];
        }
    }

    /// <inheritdoc/>
    public ValueTask ClearHistoryAsync(Guid appId, CancellationToken ct = default)
    {
        try
        {
            var filePath = GetLogFilePath(appId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                logger.LogInformation("Cleared recovery history for app {AppId}", appId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear recovery history for app {AppId}", appId);
        }

        return ValueTask.CompletedTask;
    }
}
