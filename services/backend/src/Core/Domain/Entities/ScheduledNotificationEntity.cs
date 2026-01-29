namespace Domain.Entities;

public class ScheduledNotificationEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>
    /// Notification type (e.g., "reminder", "insight", "ingestion", "sync")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Priority: 0 = Low, 1 = Normal, 2 = High, 3 = Critical
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// JSON metadata for additional data
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When the notification should be sent (UTC)
    /// </summary>
    public DateTime ScheduledFor { get; set; }

    /// <summary>
    /// Cron expression for recurring notifications (e.g., "0 9 * * 1" for every Monday at 9 AM)
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Whether this is a recurring notification
    /// </summary>
    public bool IsRecurring { get; set; } = false;

    /// <summary>
    /// Last time this notification was executed (for recurring)
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Next scheduled execution time (for recurring)
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Current status of the notification
    /// </summary>
    public ScheduledNotificationStatus Status { get; set; } = ScheduledNotificationStatus.Pending;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Last error message if failed
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Source service that created this notification (e.g., "romy-bot")
    /// </summary>
    public string? SourceService { get; set; }

    /// <summary>
    /// External reference ID from the source service
    /// </summary>
    public string? SourceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ScheduledNotificationStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
