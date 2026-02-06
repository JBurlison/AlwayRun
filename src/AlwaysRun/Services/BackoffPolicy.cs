namespace AlwaysRun.Services;

/// <summary>
/// Encapsulates exponential backoff logic for process restart scheduling.
/// </summary>
public sealed class BackoffPolicy
{
    private static readonly Random Jitter = new();

    /// <summary>
    /// Initial delay before first restart attempt.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum delay cap for backoff.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Duration the process must run healthy before resetting attempt count.
    /// </summary>
    public TimeSpan HealthyResetThreshold { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Multiplier for exponential growth.
    /// </summary>
    public double Multiplier { get; init; } = 2.0;

    /// <summary>
    /// Jitter percentage (+/- range) to apply to delays.
    /// </summary>
    public double JitterPercentage { get; init; } = 0.1;

    /// <summary>
    /// Calculates the next delay based on attempt count with optional jitter.
    /// </summary>
    /// <param name="attemptCount">Number of restart attempts (0-based).</param>
    /// <param name="addJitter">Whether to add random jitter to the delay.</param>
    /// <param name="customInitialDelay">Optional custom initial delay to use instead of the default.</param>
    /// <returns>The delay duration before next restart attempt.</returns>
    public TimeSpan GetNextDelay(int attemptCount, bool addJitter = true, TimeSpan? customInitialDelay = null)
    {
        if (attemptCount < 0)
        {
            attemptCount = 0;
        }

        var baseDelay = customInitialDelay ?? InitialDelay;

        // Calculate exponential delay: baseDelay * (Multiplier ^ attemptCount)
        var delayMs = baseDelay.TotalMilliseconds * Math.Pow(Multiplier, attemptCount);

        // Cap at maximum
        delayMs = Math.Min(delayMs, MaxDelay.TotalMilliseconds);

        if (addJitter)
        {
            // Add jitter: +/- JitterPercentage
            var jitterRange = delayMs * JitterPercentage;
            var jitterOffset = (Jitter.NextDouble() * 2 - 1) * jitterRange;
            delayMs += jitterOffset;

            // Ensure we don't go below zero or above max
            delayMs = Math.Max(0, Math.Min(delayMs, MaxDelay.TotalMilliseconds));
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Determines if the attempt count should be reset based on process uptime.
    /// </summary>
    /// <param name="processStartTime">When the process started.</param>
    /// <returns>True if the process has run long enough to reset backoff.</returns>
    public bool ShouldResetAttempts(DateTimeOffset processStartTime)
    {
        var uptime = DateTimeOffset.UtcNow - processStartTime;
        return uptime >= HealthyResetThreshold;
    }
}
