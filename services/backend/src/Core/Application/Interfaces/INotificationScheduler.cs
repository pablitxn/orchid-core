namespace Application.Interfaces;

/// <summary>
/// Port for scheduling notifications for future delivery
/// </summary>
public interface INotificationScheduler
{
    /// <summary>
    /// Schedule a notification for future delivery
    /// </summary>
    Task<Guid> ScheduleAsync(
        Guid userId,
        string type,
        string title,
        string message,
        DateTime scheduledFor,
        int priority = 1,
        string? metadata = null,
        string? sourceService = null,
        string? sourceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule a recurring notification using cron expression
    /// </summary>
    Task<Guid> ScheduleRecurringAsync(
        Guid userId,
        string type,
        string title,
        string message,
        string cronExpression,
        int priority = 1,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a scheduled notification
    /// </summary>
    Task CancelAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
