using AlwaysRun.Models;

namespace AlwaysRun.Services;

/// <summary>
/// Event arguments for application status changes.
/// </summary>
public sealed class AppStatusChangedEventArgs : EventArgs
{
    public required Guid AppId { get; init; }
    public required AppStatus Status { get; init; }
    public DateTimeOffset? LastStartTime { get; init; }
    public DateTimeOffset? LastExitTime { get; init; }
    public int? LastExitCode { get; init; }
    public string? Error { get; init; }

    public static AppStatusChangedEventArgs FromStatus(
        Guid appId,
        AppStatus status,
        DateTimeOffset? lastStartTime = null,
        DateTimeOffset? lastExitTime = null,
        int? lastExitCode = null,
        string? error = null) => new()
        {
            AppId = appId,
            Status = status,
            LastStartTime = lastStartTime,
            LastExitTime = lastExitTime,
            LastExitCode = lastExitCode,
            Error = error
        };
}
