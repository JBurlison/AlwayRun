using AlwaysRun.Models;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for reading and writing per-application recovery history logs.
/// </summary>
public interface IRecoveryLogService
{
    /// <summary>
    /// Appends a recovery event to the log for a specific application.
    /// </summary>
    ValueTask LogEventAsync(Guid appId, RecoveryEvent recoveryEvent, CancellationToken ct = default);

    /// <summary>
    /// Reads the recovery history for a specific application.
    /// </summary>
    /// <param name="appId">The application identifier.</param>
    /// <param name="maxEntries">Maximum number of entries to return (most recent first).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<IReadOnlyList<RecoveryEvent>> GetHistoryAsync(Guid appId, int maxEntries = 500, CancellationToken ct = default);

    /// <summary>
    /// Deletes the recovery log for a specific application.
    /// </summary>
    ValueTask ClearHistoryAsync(Guid appId, CancellationToken ct = default);
}
