namespace Application.Interfaces;

/// <summary>
/// Port for sending real-time credit-related notifications
/// </summary>
public interface ICreditNotificationPort
{
    /// <summary>
    /// Notify a user about credit balance update
    /// </summary>
    Task NotifyCreditBalanceUpdatedAsync(
        Guid userId, 
        int newBalance, 
        bool hasUnlimitedCredits,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify a user about credit consumption
    /// </summary>
    Task NotifyCreditConsumedAsync(
        Guid userId, 
        int amount, 
        string resourceType, 
        string resourceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify a user about low credit warning
    /// </summary>
    Task NotifyLowCreditWarningAsync(
        Guid userId, 
        int currentBalance, 
        int threshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify a user about credits being added
    /// </summary>
    Task NotifyCreditsAddedAsync(
        Guid userId,
        int amount,
        int newBalance,
        bool hasUnlimitedCredits,
        CancellationToken cancellationToken = default);
}