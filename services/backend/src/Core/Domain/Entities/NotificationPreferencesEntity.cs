namespace Domain.Entities;

public class NotificationPreferencesEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>
    /// Master toggle for all notifications
    /// </summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Start of quiet hours (e.g., 22:00). Notifications will be deferred during quiet hours.
    /// </summary>
    public TimeOnly? QuietHoursStart { get; set; }

    /// <summary>
    /// End of quiet hours (e.g., 08:00)
    /// </summary>
    public TimeOnly? QuietHoursEnd { get; set; }

    /// <summary>
    /// User timezone in IANA format (e.g., "America/New_York", "Europe/London")
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// JSON array of enabled notification types: ["reminders", "insights", "ingestion", "sync"]
    /// </summary>
    public string EnabledTypes { get; set; } = "[\"reminders\",\"insights\",\"ingestion\",\"sync\"]";

    /// <summary>
    /// Whether to send notifications via Matrix bridge
    /// </summary>
    public bool MatrixNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to send notifications via email (future feature)
    /// </summary>
    public bool EmailNotificationsEnabled { get; set; } = false;

    /// <summary>
    /// Email digest frequency: "daily", "weekly", "none"
    /// </summary>
    public string? EmailDigestFrequency { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
