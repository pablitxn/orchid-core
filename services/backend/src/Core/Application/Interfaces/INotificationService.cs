namespace Application.Interfaces;

public interface INotificationService
{
    Task SendLowCreditAlertAsync(Guid userId, int remainingCredits, int threshold, CancellationToken cancellationToken = default);
    
    Task SendCreditExhaustedAlertAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task SendCreditAddedNotificationAsync(Guid userId, int creditsAdded, int newBalance, CancellationToken cancellationToken = default);
    
    Task SendPurchaseConfirmationAsync(Guid userId, string itemType, string itemName, int creditsCost, CancellationToken cancellationToken = default);
    
    Task SendSubscriptionExpiringAlertAsync(Guid userId, DateTime expirationDate, CancellationToken cancellationToken = default);
    
    Task SendSubscriptionExpiredAlertAsync(Guid userId, CancellationToken cancellationToken = default);
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
    Error
}