using AlwaysRun.Infrastructure;

namespace AlwaysRun.Services;

/// <summary>
/// Abstraction for enabling/disabling auto-start on Windows login.
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// Sets whether the application should start on Windows user login.
    /// </summary>
    ValueTask<Result> SetEnabledAsync(bool enabled, CancellationToken ct = default);

    /// <summary>
    /// Gets whether auto-start is currently enabled.
    /// </summary>
    ValueTask<bool> IsEnabledAsync(CancellationToken ct = default);
}
