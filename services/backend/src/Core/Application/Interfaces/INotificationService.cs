namespace Application.Interfaces;

public interface INotificationService
{
    Task SendLowCreditAlertAsync(Guid userId, int remainingCredits, int threshold, CancellationToken cancellationToken = default);
    
    Task SendCreditExhaustedAlertAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task SendCreditAddedNotificationAsync(Guid userId, int creditsAdded, int newBalance, CancellationToken cancellationToken = default);
    
    Task SendPurchaseConfirmationAsync(Guid userId, string itemType, string itemName, int creditsCost, CancellationToken cancellationToken = default);
    
    Task SendSubscriptionExpiringAlertAsync(Guid userId, DateTime expirationDate, CancellationToken cancellationToken = default);
    
    Task SendSubscriptionExpiredAlertAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a general notification with type checking and preference validation
    /// </summary>
    Task SendNotificationAsync(
        Guid userId,
        string type,
        string title,
        string message,
        int priority = 1,
        string? actionUrl = null,
        string? actionText = null,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send reminder notification (deadline, creation time block)
    /// </summary>
    Task SendReminderAsync(
        Guid userId,
        string title,
        string message,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send automatic insight notification (performance, trends)
    /// </summary>
    Task SendInsightAsync(
        Guid userId,
        string title,
        string message,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send ingestion confirmation notification
    /// </summary>
    Task SendIngestionConfirmationAsync(
        Guid userId,
        string itemType,
        string itemName,
        string[]? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send sync alert notification
    /// </summary>
    Task SendSyncAlertAsync(
        Guid userId,
        string serviceName,
        bool success,
        string message,
        CancellationToken cancellationToken = default);
}

public enum NotificationType
{
    LowCreditAlert,
    CreditExhausted,
    CreditAdded,
    PurchaseConfirmation,
    SubscriptionExpiring,
    SubscriptionExpired,
    GeneralInfo,
    Warning,
    Error,

    // Proactive notification types
    Reminder,
    Insight,
    IngestionConfirmation,
    SyncAlert,
    DeadlineApproaching,
    WeeklyPerformanceSummary,
    TrendAlert
}