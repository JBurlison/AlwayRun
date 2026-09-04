namespace AlwaysRun.Models;

/// <summary>
/// Root configuration record for JSON persistence.
/// </summary>
public record AppConfiguration
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public List<ManagedAppConfig> Apps { get; init; } = [];
    public bool AutoStartEnabled { get; init; } = true;
    public bool ExitOnClose { get; init; } = false;
    public bool AutoRestartPaused { get; init; } = false;

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static AppConfiguration CreateDefault() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Apps = [],
        AutoStartEnabled = true,
        ExitOnClose = false,
        AutoRestartPaused = false
    };
}
